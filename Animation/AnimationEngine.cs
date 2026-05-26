// Animation/AnimationEngine.cs
// Orchestrates all QuoteSprites: timing, physics, transitions, keyboard commands.
using System.Diagnostics;
using System.Drawing;
using QuoteScreensaver.Models;
using QuoteScreensaver.Services;

namespace QuoteScreensaver.Animation;

/// <summary>
/// The heart of the screensaver animation.  Call <see cref="Update"/> on every
/// frame tick and <see cref="Draw"/> inside OnPaint. Thread-safe for the single
/// UI thread; background GitHub loading notifies via <see cref="QuotesRefreshed"/>.
/// </summary>
public sealed class AnimationEngine : IDisposable
{
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly AppSettings       _settings;
    private readonly QuoteSetManager   _quoteManager;

    // ── Sprites ───────────────────────────────────────────────────────────────
    private readonly List<QuoteSprite> _sprites = new();

    // ── Timing ────────────────────────────────────────────────────────────────
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private float              _lastTick = 0f;
    private float              _visibleTimer = 0f;   // how long sprites have been Visible
    private bool               _switchRequested = false;
    private bool               _prevRequested   = false;

    // ── State ─────────────────────────────────────────────────────────────────
    private RectangleF         _screenBounds;
    private bool               _initialized  = false;
    private bool               _disposed     = false;
    public  bool               IsPaused      { get; private set; }
    public  bool               ShowAuthor    { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired when background GitHub load merges new quotes in.</summary>
    public event EventHandler? QuotesRefreshed;

    // ── Help overlay ──────────────────────────────────────────────────────────
    public bool ShowHelp { get; private set; }

    // ── Constructor ───────────────────────────────────────────────────────────
    public AnimationEngine(AppSettings settings, QuoteSetManager quoteManager)
    {
        _settings     = settings;
        ShowAuthor    = settings.ShowAuthor;
        _quoteManager = quoteManager;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Must be called once before the first Update, with the screen rectangle.</summary>
    public void Initialize(RectangleF screenBounds)
    {
        _screenBounds = screenBounds;
        _initialized  = true;
        SpawnGroup();
    }

    /// <summary>
    /// Call this every timer tick (from UI thread).
    /// Returns true if the screen needs repainting.
    /// </summary>
    public bool Update()
    {
        if (!_initialized) return false;

        float now = (float)_clock.Elapsed.TotalSeconds;
        float dt  = Math.Min(now - _lastTick, 0.1f); // cap at 100ms to handle focus loss
        _lastTick = now;

        if (IsPaused) return false;

        // ── Handle switch requests ────────────────────────────────────────────
        if (_switchRequested || _prevRequested)
        {
            bool prev = _prevRequested;
            _switchRequested = false;
            _prevRequested   = false;
            BeginFadeOut(prev ? FadeDirection.Previous : FadeDirection.Next);
        }

        // ── Auto-advance timer ────────────────────────────────────────────────
        if (AllSpritesVisible())
        {
            _visibleTimer += dt;
            if (_visibleTimer >= _settings.DisplayDurationSeconds)
            {
                _visibleTimer = 0f;
                BeginFadeOut(FadeDirection.Next);
            }
        }

        // ── Update all sprites ────────────────────────────────────────────────
        var readOnlySprites = (IReadOnlyList<QuoteSprite>)_sprites;
        foreach (var sprite in _sprites)
            sprite.Update(dt, _screenBounds, _settings.FadeDurationSeconds,
                          readOnlySprites, _settings.EnableCollisions);

        // ── Check if fade-out completed → spawn new group ─────────────────────
        if (AllSpritesHidden() && _sprites.Count > 0)
            SpawnGroup();

        return true;
    }

    /// <summary>Draws all sprites onto the provided Graphics context.</summary>
    public void Draw(Graphics g, Rectangle screenRect)
    {
        // Background
        g.Clear(_settings.BackgroundColor);

        foreach (var sprite in _sprites)
            sprite.Draw(g, _settings.TextColor, _settings.ShowTextShadow);

        if (ShowHelp) DrawHelpOverlay(g, screenRect);
    }

    // ── Keyboard commands ─────────────────────────────────────────────────────
    public void RequestNext()      => _switchRequested = true;
    public void RequestPrevious()  => _prevRequested   = true;
    public void TogglePause()      => IsPaused = !IsPaused;
    public void ToggleHelp()       => ShowHelp = !ShowHelp;

    public void ToggleAuthor()
    {
        ShowAuthor = !ShowAuthor;
        foreach (var s in _sprites)
            s.RefreshDisplayText(ShowAuthor);
    }

    public async void ReloadQuotes()
    {
        await _quoteManager.ReloadAsync();
        if (_disposed) return; // Guard: form may have closed during async load
        RequestNext();
        QuotesRefreshed?.Invoke(this, EventArgs.Empty);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private enum FadeDirection { Next, Previous }

    private void BeginFadeOut(FadeDirection dir)
    {
        // Tag sprites so we know which direction to go after fade completes
        _pendingDirection = dir;
        foreach (var s in _sprites) s.StartFadeOut();
        _visibleTimer = 0f;
    }

    private FadeDirection _pendingDirection = FadeDirection.Next;

    private void SpawnGroup()
    {
        // Dispose old sprite fonts before replacing them
        foreach (var s in _sprites) s.Font.Dispose();
        _sprites.Clear();

        int count = Math.Clamp(_settings.MaxQuotesOnScreen, 1, 3);

        Quote[]? group = _pendingDirection == FadeDirection.Previous
            ? _quoteManager.PreviousGroup()
            : null;

        group ??= _quoteManager.NextGroup(count);
        if (group.Length == 0) group = _quoteManager.NextGroup(1); // safety

        float baseSpeed = 40f * _settings.SpeedMultiplier;

        foreach (var q in group)
        {
            var sprite = new QuoteSprite(q, ShowAuthor, _screenBounds, baseSpeed,
                                         _settings.FontFamily);
            sprite.StartFadeIn();
            _sprites.Add(sprite);
        }
    }

    private bool AllSpritesVisible() =>
        _sprites.Count > 0 && _sprites.All(s => s.State == FadeState.Visible);

    private bool AllSpritesHidden() =>
        _sprites.Count > 0 && _sprites.All(s => s.IsFullyHidden);

    // ── Help overlay ──────────────────────────────────────────────────────────

    private static void DrawHelpOverlay(Graphics g, Rectangle screen)
    {
        // Semi-transparent dark panel
        int panelW = Math.Min(520, screen.Width - 40);
        int panelH = 320;
        int panelX = (screen.Width  - panelW) / 2;
        int panelY = (screen.Height - panelH) / 2;

        using var panelBrush = new SolidBrush(Color.FromArgb(210, 10, 10, 30));
        g.FillRoundedRectangle(panelBrush, new Rectangle(panelX, panelY, panelW, panelH), 18);

        using var border = new Pen(Color.FromArgb(160, 180, 160, 120), 1.5f);
        g.DrawRoundedRectangle(border, new Rectangle(panelX, panelY, panelW, panelH), 18);

        using var titleFont = new Font("Segoe UI", 16f, FontStyle.Bold);
        using var keyFont   = new Font("Segoe UI", 11f, FontStyle.Regular);
        using var whiteBr   = new SolidBrush(Color.FromArgb(240, 230, 215));
        using var dimBr     = new SolidBrush(Color.FromArgb(180, 210, 200, 180));

        var centerSF = new StringFormat { Alignment = StringAlignment.Center };

        g.DrawString("Keyboard Shortcuts", titleFont, whiteBr,
                     new RectangleF(panelX, panelY + 16, panelW, 28), centerSF);

        var shortcuts = new[]
        {
            ("Space / →",        "Next quote"),
            ("←",                "Previous quote"),
            ("P",                "Pause / Resume"),
            ("R",                "Reload quotes"),
            ("A",                "Toggle author credit"),
            ("H",                "Show / Hide this help"),
            ("Esc / Mouse Move", "Exit screensaver"),
        };

        float rowY = panelY + 55;
        float colK = panelX + 30;
        float colD = panelX + panelW / 2 + 10;
        float rowH = 32f;

        using var keyBg = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
        using var keyFont2 = new Font("Consolas", 10.5f, FontStyle.Regular);

        foreach (var (key, desc) in shortcuts)
        {
            // Key badge
            var keyRect = new RectangleF(colK, rowY + 3, 180, 22);
            g.FillRoundedRectangle(keyBg, keyRect.ToRectangle(), 5);
            g.DrawString(key,  keyFont2, whiteBr, keyRect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            g.DrawString(desc, keyFont,  dimBr,   new PointF(colD, rowY));
            rowY += rowH;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var s in _sprites) s.Font.Dispose();
        _sprites.Clear();
    }
}

// ── Graphics extension helpers ────────────────────────────────────────────────
internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
    {
        using var path = CreateRoundedPath(rect, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
    {
        using var path = CreateRoundedPath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedPath(Rectangle r, int rad)
    {
        int d = rad * 2;
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        p.AddArc(r.X,               r.Y,                d, d, 180, 90);
        p.AddArc(r.Right - d,       r.Y,                d, d, 270, 90);
        p.AddArc(r.Right - d,       r.Bottom - d,       d, d,   0, 90);
        p.AddArc(r.X,               r.Bottom - d,       d, d,  90, 90);
        p.CloseFigure();
        return p;
    }

    public static Rectangle ToRectangle(this RectangleF r) =>
        new((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
}
