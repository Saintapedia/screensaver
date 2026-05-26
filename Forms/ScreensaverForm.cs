// Forms/ScreensaverForm.cs
// Full-screen double-buffered screensaver form with 60 FPS animation loop.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QuoteScreensaver.Animation;
using QuoteScreensaver.Models;
using QuoteScreensaver.Services;

namespace QuoteScreensaver.Forms;

/// <summary>
/// Full-screen screensaver window.  One instance per physical monitor.
/// The <paramref name="isPrimary"/> instance owns the animation engine and
/// cursor lifecycle; secondary instances render from the same engine so all
/// monitors stay in sync.
/// </summary>
public sealed class ScreensaverForm : Form
{
    // ── P/Invoke ──────────────────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern bool SetParent(nint child, nint newParent);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out RECT rc);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    private const uint SWP_NOZORDER    = 0x0004;
    private const uint SWP_SHOWWINDOW  = 0x0040;
    private static readonly nint HWND_TOPMOST = new(-1);

    // ── Fields ────────────────────────────────────────────────────────────────
    private readonly AppSettings      _settings;
    private readonly QuoteSetManager  _quoteManager;
    private readonly AnimationEngine  _engine;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly bool             _isPrimary;
    private readonly bool             _isPreview;
    private readonly nint             _previewHwnd;

    private Point _lastMouse;
    private bool  _firstMouseEvent = true;
    private bool  _exitRequested   = false;
    private int   _cursorHidden    = 0;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="screen">The monitor to cover (null in preview mode).</param>
    /// <param name="settings">App settings.</param>
    /// <param name="quoteManager">Quote source manager.</param>
    /// <param name="isPrimary">True for the form that owns the engine / cursor.</param>
    /// <param name="previewHwnd">Non-zero handle for /p preview mode.</param>
    public ScreensaverForm(
        Screen?          screen,
        AppSettings      settings,
        QuoteSetManager  quoteManager,
        bool             isPrimary,
        nint             previewHwnd = 0)
    {
        _settings     = settings;
        _quoteManager = quoteManager;
        _isPrimary    = isPrimary;
        _isPreview    = previewHwnd != 0;
        _previewHwnd  = previewHwnd;

        _engine = new AnimationEngine(settings, quoteManager);

        // ── Form style ────────────────────────────────────────────────────────
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint  |
                 ControlStyles.UserPaint, true);
        UpdateStyles();

        BackColor        = settings.BackgroundColor;
        FormBorderStyle  = FormBorderStyle.None;
        ShowInTaskbar    = false;
        TopMost          = !_isPreview;
        Text             = "Quote Screensaver";
        Cursor           = Cursors.None;

        if (_isPreview)
        {
            SetupPreviewLayout();
        }
        else if (screen != null)
        {
            // Cover the exact physical screen bounds (handles per-monitor DPI)
            Bounds = screen.Bounds;
        }

        // 60 FPS timer
        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += TimerTick;
        _timer.Start();
    }

    // ── Preview layout ────────────────────────────────────────────────────────

    private void SetupPreviewLayout()
    {
        if (!GetClientRect(_previewHwnd, out var rc)) return;
        SetParent(Handle, _previewHwnd);
        FormBorderStyle = FormBorderStyle.None;
        Location        = Point.Empty;
        Size            = new Size(rc.Right - rc.Left, rc.Bottom - rc.Top);
        TopMost         = false;
    }

    // ── Lifetime ──────────────────────────────────────────────────────────────

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Make this window the topmost on its screen (workaround for taskbar coverage)
        if (!_isPreview && _isPrimary)
            SetWindowPos(Handle, HWND_TOPMOST, Left, Top, Width, Height, SWP_SHOWWINDOW);

        // Hide mouse cursor (only once — WinAPI ref-counts ShowCursor)
        if (_isPrimary && !_isPreview)
        {
            while (ShowCursor(false) >= 0) _cursorHidden++;
        }

        _lastMouse = Cursor.Position;

        // Initialise the animation engine with our client rectangle
        var bounds = new RectangleF(0, 0, ClientSize.Width, ClientSize.Height);
        _engine.Initialize(bounds);

        // Load quotes (sync for local / built-ins; GitHub fires in background)
        if (_isPrimary) _quoteManager.InitializeSync();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();

        if (_isPrimary && _cursorHidden > 0)
        {
            for (int i = 0; i < _cursorHidden; i++) ShowCursor(true);
        }

        _engine.Dispose();
        base.OnFormClosed(e);
    }

    // ── P/Invoke ShowCursor ───────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern int ShowCursor(bool bShow);

    // ── Animation loop ────────────────────────────────────────────────────────

    private void TimerTick(object? sender, EventArgs e)
    {
        if (_exitRequested) { Close(); return; }

        _engine.Update();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        _engine.Draw(g, ClientRectangle);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isPreview) { base.OnMouseMove(e); return; }

        // Swallow the spurious first move event (Windows fires one on window creation)
        if (_firstMouseEvent) { _firstMouseEvent = false; _lastMouse = e.Location; return; }

        if (Math.Abs(e.X - _lastMouse.X) > 4 || Math.Abs(e.Y - _lastMouse.Y) > 4)
            _exitRequested = true;

        _lastMouse = e.Location;
        base.OnMouseMove(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!_isPreview) _exitRequested = true;
        base.OnMouseDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!_isPreview) _exitRequested = true;
        base.OnMouseWheel(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_isPreview) { base.OnKeyDown(e); return; }

        switch (e.KeyCode)
        {
            case Keys.Escape:
                _exitRequested = true;
                break;
            case Keys.Space:
            case Keys.Right:
                _engine.RequestNext();
                break;
            case Keys.Left:
                _engine.RequestPrevious();
                break;
            case Keys.P:
                _engine.TogglePause();
                break;
            case Keys.R:
                _engine.ReloadQuotes();
                break;
            case Keys.A:
                _engine.ToggleAuthor();
                break;
            case Keys.H:
                _engine.ToggleHelp();
                break;
            default:
                _exitRequested = true;
                break;
        }

        e.Handled = true;
        base.OnKeyDown(e);
    }

    // ── Resize (DPI change / resolution change while running) ─────────────────

    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        if (ClientSize.Width > 0 && ClientSize.Height > 0)
            _engine.Initialize(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height));
    }

    // ── CreateParams: composite + no taskbar ─────────────────────────────────

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_EX_TOOLWINDOW: no taskbar entry or Alt-Tab appearance
            cp.ExStyle |= 0x00000080;
            return cp;
        }
    }
}
