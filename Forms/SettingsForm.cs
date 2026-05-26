// Forms/SettingsForm.cs
// Comprehensive settings dialog (/c mode).  All layout is programmatic — no designer.
using System.Drawing;
using System.Windows.Forms;
using QuoteScreensaver.Models;
using QuoteScreensaver.Services;

namespace QuoteScreensaver.Forms;

/// <summary>
/// Multi-tab settings dialog.
/// Tabs: General | Sources | Appearance | Help
/// </summary>
public sealed class SettingsForm : Form
{
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly AppSettings      _settings;
    private readonly SettingsManager  _settingsManager;
    private readonly QuoteSetManager  _quoteManager;

    // ── Tab: General ──────────────────────────────────────────────────────────
    private NumericUpDown _nudDuration   = null!;
    private TrackBar      _trkDuration   = null!;
    private NumericUpDown _nudFade       = null!;
    private TrackBar      _trkFade       = null!;
    private TrackBar      _trkSpeed      = null!;
    private Label         _lblSpeedVal   = null!;
    private NumericUpDown _nudMaxQuotes  = null!;
    private CheckBox      _chkCollisions = null!;

    // ── Tab: Sources ──────────────────────────────────────────────────────────
    private RadioButton   _rbLocal       = null!;
    private RadioButton   _rbGitHub      = null!;
    private RadioButton   _rbBoth        = null!;
    private TextBox       _txtLocalPath  = null!;
    private Button        _btnBrowse     = null!;
    private TextBox       _txtGitHub     = null!;
    private Button        _btnTestGH     = null!;
    private Label         _lblGHStatus   = null!;
    private ComboBox      _cmbPresets    = null!;
    private ComboBox      _cmbCacheInt   = null!;
    private Button        _btnRefreshGH  = null!;
    private ListBox       _lstSets       = null!;

    // ── Tab: Appearance ───────────────────────────────────────────────────────
    private Button        _btnBgColor    = null!;
    private Button        _btnTxtColor   = null!;
    private CheckBox      _chkAuthor     = null!;
    private CheckBox      _chkShadow     = null!;
    private ComboBox      _cmbFont       = null!;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsForm(AppSettings settings, SettingsManager settingsManager,
                        QuoteSetManager quoteManager)
    {
        _settings        = settings;
        _settingsManager = settingsManager;
        _quoteManager    = quoteManager;

        Text             = "Quote Screensaver — Settings";
        Size             = new Size(620, 560);
        MinimumSize      = new Size(580, 500);
        StartPosition    = FormStartPosition.CenterScreen;
        FormBorderStyle  = FormBorderStyle.FixedDialog;
        MaximizeBox      = false;
        MinimizeBox      = false;
        Font             = new Font("Segoe UI", 9.5f);
        BackColor        = Color.FromArgb(30, 30, 40);
        ForeColor        = Color.FromArgb(220, 215, 200);

        BuildUI();
        PopulateValues();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var tabs = new TabControl
        {
            Dock      = DockStyle.Fill,
            Padding   = new Point(12, 6),
            Font      = new Font("Segoe UI", 9.5f),
        };

        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildSourcesTab());
        tabs.TabPages.Add(BuildAppearanceTab());
        tabs.TabPages.Add(BuildHelpTab());

        // Bottom button strip
        var btnSave = MakeButton("Save", DialogResult.OK);
        btnSave.Click += (_, _) => SaveAndClose();

        var btnCancel = MakeButton("Cancel", DialogResult.Cancel);

        var bottomPanel = new Panel
        {
            Dock   = DockStyle.Bottom,
            Height = 50,
        };

        btnSave.Location   = new Point(420, 10);
        btnSave.Size       = new Size(84, 30);
        btnCancel.Location = new Point(512, 10);
        btnCancel.Size     = new Size(84, 30);

        bottomPanel.Controls.Add(btnSave);
        bottomPanel.Controls.Add(btnCancel);

        Controls.Add(tabs);
        Controls.Add(bottomPanel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    // ── Tab: General ──────────────────────────────────────────────────────────

    private TabPage BuildGeneralTab()
    {
        var page = MakePage("⚙  General");
        int y = 18;

        // Display Duration
        page.Controls.Add(MakeLabel("Quote display duration (seconds):", 16, y));
        _trkDuration = MakeTrackBar(1, 120, 16, y + 22, 350);
        _nudDuration = MakeNud(1, 120, 370, y + 22, 65);
        page.Controls.AddRange(new Control[] { _trkDuration, _nudDuration });
        _trkDuration.ValueChanged += (_, _) => _nudDuration.Value = _trkDuration.Value;
        _nudDuration.ValueChanged += (_, _) => _trkDuration.Value = (int)_nudDuration.Value;
        y += 60;

        // Fade Duration — NUD shows 0.1–5.0 s in 0.1 increments; trackbar is ×10
        page.Controls.Add(MakeLabel("Fade duration (seconds, 0.1–5.0):", 16, y));
        _trkFade = MakeTrackBar(1, 50, 16, y + 22, 350);   // internal: 1..50 = 0.1..5.0 s
        _nudFade = MakeNud(1, 50, 370, y + 22, 65);
        _nudFade.DecimalPlaces = 1;
        _nudFade.Minimum       = 0.1m;
        _nudFade.Maximum       = 5.0m;
        _nudFade.Increment     = 0.1m;
        page.Controls.AddRange(new Control[] { _trkFade, _nudFade });
        _trkFade.ValueChanged += (_, _) => _nudFade.Value = _trkFade.Value / 10m;
        _nudFade.ValueChanged += (_, _) => _trkFade.Value = (int)Math.Round(_nudFade.Value * 10);
        y += 60;

        // Speed
        page.Controls.Add(MakeLabel("Movement speed:", 16, y));
        _trkSpeed = MakeTrackBar(1, 30, 16, y + 22, 350);   // ×0.1 = 0.1..3.0×
        _lblSpeedVal = new Label
        {
            Location  = new Point(372, y + 27),
            Size      = new Size(70, 20),
            ForeColor = Color.FromArgb(200, 190, 170),
        };
        page.Controls.AddRange(new Control[] { _trkSpeed, _lblSpeedVal });
        _trkSpeed.ValueChanged += (_, _) => _lblSpeedVal.Text = $"{_trkSpeed.Value / 10.0:F1}×";
        y += 60;

        // Max quotes
        page.Controls.Add(MakeLabel("Max quotes on screen at once:", 16, y));
        _nudMaxQuotes = MakeNud(1, 3, 280, y, 55);
        page.Controls.Add(_nudMaxQuotes);
        y += 38;

        // Collisions
        _chkCollisions = MakeCheckBox("Enable collision physics between quotes", 16, y);
        page.Controls.Add(_chkCollisions);

        return page;
    }

    // ── Tab: Sources ──────────────────────────────────────────────────────────

    private TabPage BuildSourcesTab()
    {
        var page = MakePage("📂  Sources");
        int y = 18;

        // Source mode
        page.Controls.Add(MakeLabel("Quote source:", 16, y));
        _rbLocal  = MakeRadio("Local files only",      16,  y + 22);
        _rbGitHub = MakeRadio("GitHub only",           170, y + 22);
        _rbBoth   = MakeRadio("Both local + GitHub",   290, y + 22);
        page.Controls.AddRange(new Control[] { _rbLocal, _rbGitHub, _rbBoth });
        y += 58;

        // Local path
        page.Controls.Add(MakeLabel("Local quotes folder:", 16, y));
        _txtLocalPath = MakeTextBox(16, y + 22, 370);
        _btnBrowse    = MakeSmallButton("Browse…", 394, y + 20);
        _btnBrowse.Click += BrowseLocalFolder;
        page.Controls.AddRange(new Control[] { _txtLocalPath, _btnBrowse });
        y += 60;

        // GitHub URL
        page.Controls.Add(MakeLabel("GitHub URL (raw file or folder API):", 16, y));

        // Presets combo
        _cmbPresets = new ComboBox
        {
            Location      = new Point(16, y + 22),
            Size          = new Size(200, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            ForeColor     = Color.FromArgb(220, 215, 200),
            BackColor     = Color.FromArgb(50, 50, 65),
        };
        foreach (var (label, _) in AppSettings.GitHubPresets)
            _cmbPresets.Items.Add(label);
        _cmbPresets.SelectedIndexChanged += (_, _) =>
        {
            int idx = _cmbPresets.SelectedIndex;
            if (idx >= 0 && idx < AppSettings.GitHubPresets.Count)
            {
                var url = AppSettings.GitHubPresets[idx].Url;
                if (!string.IsNullOrEmpty(url)) _txtGitHub.Text = url;
            }
        };
        page.Controls.Add(_cmbPresets);
        y += 22;

        _txtGitHub  = MakeTextBox(16, y + 22, 370);
        _btnTestGH  = MakeSmallButton("Test…", 394, y + 20);
        _lblGHStatus = new Label
        {
            Location  = new Point(16, y + 50),
            Size      = new Size(450, 20),
            ForeColor = Color.FromArgb(140, 200, 140),
            Font      = new Font("Segoe UI", 8.5f),
        };
        _btnTestGH.Click += TestGitHubConnection;
        page.Controls.AddRange(new Control[] { _txtGitHub, _btnTestGH, _lblGHStatus });
        y += 75;

        // Cache refresh
        page.Controls.Add(MakeLabel("Cache refresh interval:", 16, y));
        _cmbCacheInt = new ComboBox
        {
            Location      = new Point(200, y),
            Size          = new Size(130, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            ForeColor     = Color.FromArgb(220, 215, 200),
            BackColor     = Color.FromArgb(50, 50, 65),
        };
        foreach (var v in Enum.GetValues<CacheRefreshInterval>())
            _cmbCacheInt.Items.Add(v.ToString());

        _btnRefreshGH = MakeSmallButton("Refresh now", 340, y - 2);
        _btnRefreshGH.Click += async (_, _) =>
        {
            _btnRefreshGH.Enabled = false;
            _btnRefreshGH.Text    = "Refreshing…";
            await _quoteManager.ReloadAsync();
            _btnRefreshGH.Text    = "Done ✔";
            await Task.Delay(2000);
            _btnRefreshGH.Text    = "Refresh now";
            _btnRefreshGH.Enabled = true;
            RefreshSetList();
        };
        page.Controls.AddRange(new Control[] { _cmbCacheInt, _btnRefreshGH });
        y += 38;

        // Active set list
        page.Controls.Add(MakeLabel("Loaded quote sets (check to enable):", 16, y));
        _lstSets = new ListBox
        {
            Location     = new Point(16, y + 22),
            Size         = new Size(540, 80),
            ForeColor    = Color.FromArgb(220, 215, 200),
            BackColor    = Color.FromArgb(40, 40, 55),
            SelectionMode = SelectionMode.MultiSimple,
        };
        page.Controls.Add(_lstSets);

        return page;
    }

    // ── Tab: Appearance ───────────────────────────────────────────────────────

    private TabPage BuildAppearanceTab()
    {
        var page = MakePage("🎨  Appearance");
        int y = 18;

        // Font family
        page.Controls.Add(MakeLabel("Font family:", 16, y));
        _cmbFont = new ComboBox
        {
            Location      = new Point(130, y - 2),
            Size          = new Size(200, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            ForeColor     = Color.FromArgb(220, 215, 200),
            BackColor     = Color.FromArgb(50, 50, 65),
        };
        foreach (var ff in new[] { "Segoe UI", "Georgia", "Palatino Linotype", "Trebuchet MS",
                                    "Arial", "Times New Roman", "Verdana", "Cambria" })
            _cmbFont.Items.Add(ff);
        page.Controls.Add(_cmbFont);
        y += 40;

        // Background color
        page.Controls.Add(MakeLabel("Background color:", 16, y));
        _btnBgColor = MakeColorButton(200, y - 3, _settings.BackgroundColor);
        _btnBgColor.Click += (_, _) => PickColor(_btnBgColor, "Choose Background Color");
        page.Controls.Add(_btnBgColor);
        y += 40;

        // Text color
        page.Controls.Add(MakeLabel("Text color:", 16, y));
        _btnTxtColor = MakeColorButton(200, y - 3, _settings.TextColor);
        _btnTxtColor.Click += (_, _) => PickColor(_btnTxtColor, "Choose Text Color");
        page.Controls.Add(_btnTxtColor);
        y += 40;

        // Checkboxes
        _chkAuthor = MakeCheckBox("Show author / source attribution", 16, y); y += 30;
        _chkShadow = MakeCheckBox("Show text drop shadow",            16, y); y += 30;
        page.Controls.AddRange(new Control[] { _chkAuthor, _chkShadow });

        // Preview hint
        var hint = new Label
        {
            Text      = "ℹ  Changes take effect immediately the next time the screensaver starts.",
            Location  = new Point(16, y + 10),
            Size      = new Size(530, 20),
            ForeColor = Color.FromArgb(140, 140, 160),
            Font      = new Font("Segoe UI", 8.5f),
        };
        page.Controls.Add(hint);

        return page;
    }

    // ── Tab: Help ─────────────────────────────────────────────────────────────

    private TabPage BuildHelpTab()
    {
        var page = MakePage("❓  Help");

        var rtb = new RichTextBox
        {
            Dock      = DockStyle.Fill,
            ReadOnly  = true,
            BackColor = Color.FromArgb(22, 22, 32),
            ForeColor = Color.FromArgb(210, 205, 190),
            Font      = new Font("Segoe UI", 9.5f),
            BorderStyle = BorderStyle.None,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
        };
        rtb.Text = HelpText;
        page.Controls.Add(rtb);
        return page;
    }

    // ── Populate / Save ───────────────────────────────────────────────────────

    private void PopulateValues()
    {
        // General
        _trkDuration.Value  = (int)Math.Clamp(_settings.DisplayDurationSeconds, 1, 120);
        _nudDuration.Value  = _trkDuration.Value;
        _trkFade.Value      = (int)Math.Clamp(_settings.FadeDurationSeconds * 10, 1, 50);
        _nudFade.Value      = Math.Clamp((decimal)_settings.FadeDurationSeconds, 0.1m, 5.0m);
        _trkSpeed.Value     = (int)Math.Clamp(_settings.SpeedMultiplier * 10, 1, 30);
        _lblSpeedVal.Text   = $"{_trkSpeed.Value / 10.0:F1}×";
        _nudMaxQuotes.Value = Math.Clamp(_settings.MaxQuotesOnScreen, 1, 3);
        _chkCollisions.Checked = _settings.EnableCollisions;

        // Sources
        _rbLocal.Checked  = _settings.SourceMode == QuoteSourceMode.Local;
        _rbGitHub.Checked = _settings.SourceMode == QuoteSourceMode.GitHub;
        _rbBoth.Checked   = _settings.SourceMode == QuoteSourceMode.Both;
        _txtLocalPath.Text = _settings.LocalQuotesFolder;
        _txtGitHub.Text    = _settings.GitHubUrl;

        var ci = (int)_settings.CacheRefresh;
        if (ci >= 0 && ci < _cmbCacheInt.Items.Count) _cmbCacheInt.SelectedIndex = ci;
        else _cmbCacheInt.SelectedIndex = 0;

        RefreshSetList();

        // Appearance
        _btnBgColor.BackColor  = _settings.BackgroundColor;
        _btnTxtColor.BackColor = _settings.TextColor;
        _chkAuthor.Checked     = _settings.ShowAuthor;
        _chkShadow.Checked     = _settings.ShowTextShadow;

        var fi = _cmbFont.Items.IndexOf(_settings.FontFamily);
        _cmbFont.SelectedIndex = fi >= 0 ? fi : 0;
    }

    private void SaveAndClose()
    {
        // General
        _settings.DisplayDurationSeconds = (float)_nudDuration.Value;
        _settings.FadeDurationSeconds    = (float)_nudFade.Value;
        _settings.SpeedMultiplier        = _trkSpeed.Value / 10f;
        _settings.MaxQuotesOnScreen      = (int)_nudMaxQuotes.Value;
        _settings.EnableCollisions       = _chkCollisions.Checked;

        // Sources
        _settings.SourceMode = _rbLocal.Checked  ? QuoteSourceMode.Local  :
                               _rbGitHub.Checked ? QuoteSourceMode.GitHub :
                                                    QuoteSourceMode.Both;
        _settings.LocalQuotesFolder = _txtLocalPath.Text.Trim();
        _settings.GitHubUrl         = _txtGitHub.Text.Trim();
        _settings.CacheRefresh      = (CacheRefreshInterval)Math.Max(0, _cmbCacheInt.SelectedIndex);

        // Enabled sets (selected items in _lstSets)
        var enabled = _lstSets.SelectedItems.Cast<string>().ToList();
        _settings.EnabledSets = string.Join(",", enabled);

        // Appearance
        _settings.BackgroundColor = _btnBgColor.BackColor;
        _settings.TextColor       = _btnTxtColor.BackColor;
        _settings.ShowAuthor      = _chkAuthor.Checked;
        _settings.ShowTextShadow  = _chkShadow.Checked;
        _settings.FontFamily      = _cmbFont.SelectedItem?.ToString() ?? "Segoe UI";

        _settingsManager.Save(_settings);
        Close();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void BrowseLocalFolder(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description         = "Select the folder containing your quote .txt / .csv files",
            UseDescriptionForTitle = true,
            SelectedPath        = _txtLocalPath.Text,
            ShowNewFolderButton = false,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtLocalPath.Text = dlg.SelectedPath;
    }

    private async void TestGitHubConnection(object? sender, EventArgs e)
    {
        _btnTestGH.Enabled = false;
        _lblGHStatus.Text  = "Testing…";
        _lblGHStatus.ForeColor = Color.FromArgb(200, 200, 100);

        using var loader = new GitHubQuoteLoader(_settingsManager.CacheDirectory);
        var error = await loader.TestConnectionAsync(_txtGitHub.Text.Trim());

        if (error == null)
        {
            _lblGHStatus.Text      = "✔  Connection successful!";
            _lblGHStatus.ForeColor = Color.FromArgb(120, 200, 120);
        }
        else
        {
            _lblGHStatus.Text      = $"✖  {error}";
            _lblGHStatus.ForeColor = Color.FromArgb(220, 100, 100);
        }

        _btnTestGH.Enabled = true;
    }

    private void RefreshSetList()
    {
        _lstSets.Items.Clear();
        var enabledSet = _settings.EnabledSets
                                  .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => s.Trim())
                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var set in _quoteManager.Sets)
        {
            int idx = _lstSets.Items.Add(set.Name);
            // Pre-select if enabled (or if all are enabled / none specified)
            if (enabledSet.Count == 0 || enabledSet.Contains(set.Name))
                _lstSets.SetSelected(idx, true);
        }
    }

    private void PickColor(Button btn, string title)
    {
        using var dlg = new ColorDialog
        {
            Color        = btn.BackColor,
            FullOpen     = true,
            AnyColor     = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            btn.BackColor = dlg.Color;
    }

    // ── Control factory helpers ───────────────────────────────────────────────

    private static TabPage MakePage(string title) => new()
    {
        Text      = title,
        BackColor = Color.FromArgb(28, 28, 38),
        ForeColor = Color.FromArgb(220, 215, 200),
        Padding   = new Padding(10),
    };

    private static Label MakeLabel(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        AutoSize  = true,
        ForeColor = Color.FromArgb(200, 195, 175),
    };

    private static TrackBar MakeTrackBar(int min, int max, int x, int y, int w) => new()
    {
        Minimum      = min,
        Maximum      = max,
        Location     = new Point(x, y),
        Size         = new Size(w, 30),
        TickFrequency = Math.Max(1, (max - min) / 10),
        TickStyle    = TickStyle.BottomRight,
    };

    private static NumericUpDown MakeNud(int min, int max, int x, int y, int w) => new()
    {
        Minimum   = min,
        Maximum   = max,
        Location  = new Point(x, y),
        Size      = new Size(w, 24),
        BackColor = Color.FromArgb(50, 50, 65),
        ForeColor = Color.FromArgb(220, 215, 200),
    };

    private static CheckBox MakeCheckBox(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        AutoSize  = true,
        ForeColor = Color.FromArgb(210, 205, 185),
    };

    private static RadioButton MakeRadio(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        AutoSize  = true,
        ForeColor = Color.FromArgb(210, 205, 185),
    };

    private static TextBox MakeTextBox(int x, int y, int w) => new()
    {
        Location  = new Point(x, y),
        Size      = new Size(w, 24),
        BackColor = Color.FromArgb(50, 50, 65),
        ForeColor = Color.FromArgb(220, 215, 200),
        BorderStyle = BorderStyle.FixedSingle,
    };

    private static Button MakeSmallButton(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(84, 26),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(55, 55, 75),
        ForeColor = Color.FromArgb(210, 205, 185),
    };

    private static Button MakeButton(string text, DialogResult dr) => new()
    {
        Text         = text,
        DialogResult = dr,
        FlatStyle    = FlatStyle.Flat,
        BackColor    = dr == DialogResult.OK
                       ? Color.FromArgb(60, 100, 60)
                       : Color.FromArgb(55, 55, 75),
        ForeColor    = Color.FromArgb(220, 215, 200),
    };

    private static Button MakeColorButton(int x, int y, Color initial) => new()
    {
        Location  = new Point(x, y),
        Size      = new Size(100, 26),
        BackColor = initial,
        FlatStyle = FlatStyle.Flat,
        Text      = "",
    };

    // ── Help text ─────────────────────────────────────────────────────────────

    private const string HelpText = @"QUOTE SCREENSAVER — HELP

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

KEYBOARD SHORTCUTS (while screensaver is running)
───────────────────────────────────────────────────
  Space / →      Next quote
  ←              Previous quote
  P              Pause / Resume animation
  R              Reload quotes from all sources
  A              Toggle author attribution
  H              Show / Hide this keyboard shortcut overlay
  Esc            Exit screensaver
  Any other key  Exit screensaver

USING LOCAL FILES
──────────────────
  • Place .txt and/or .csv files in any folder.
  • Set that folder path in the Sources tab.

  .txt format — one quote per line:
      The only way to do great work is to love what you do.
      "Life is short." — Unknown
      A quote — Author Name

  .csv format — Quote,Author columns (header optional):
      Quote,Author
      "Be the change","Mahatma Gandhi"
      "To be or not to be","Shakespeare"

USING GITHUB
─────────────
  • Enter a raw GitHub URL to a single file:
      https://raw.githubusercontent.com/user/repo/main/quotes.txt

  • Or a GitHub tree URL to a folder (auto-lists all .txt/.csv files):
      https://github.com/user/repo/tree/main/quotes/

  • Or a GitHub Contents API URL:
      https://api.github.com/repos/user/repo/contents/quotes/

  • Quotes are cached in:
      %AppData%\QuoteScreensaver\cache\

  • Use 'Test…' to verify the URL before saving.
  • Use 'Refresh now' to force re-download.

INSTALLATION GUIDE
───────────────────
  1. Build:  dotnet publish -r win-x64 -c Release --self-contained false
  2. Rename: QuoteScreensaver.exe  →  QuoteScreensaver.scr
  3. Copy  : QuoteScreensaver.scr  to  C:\Windows\System32\
  4. Right-click the .scr file → Install
     OR open Screen Saver Settings and select 'Quote Screensaver'.

  To run manually for testing:
      QuoteScreensaver.exe /s   (screensaver mode)
      QuoteScreensaver.exe /c   (settings)
      QuoteScreensaver.exe /p 0 (preview — use any non-zero HWND in practice)

TROUBLESHOOTING
────────────────
  Screensaver doesn't appear:
    • Ensure the .scr is in System32 and properly installed.
    • Run QuoteScreensaver.exe /s from a command prompt to see errors.

  No quotes showing:
    • Built-in quotes are always present as fallback.
    • Check the Sources tab and ensure the folder path is correct.
    • For GitHub: use 'Test…' to verify connectivity.

  GitHub quotes not refreshing:
    • Change Cache Refresh Interval to 'Manual' then click 'Refresh now'.
    • Or delete files in %AppData%\QuoteScreensaver\cache\.

  High CPU usage:
    • Reduce Max Quotes on Screen to 1.
    • Increase Display Duration to reduce transition frequency.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Version 1.0  |  .NET 8 / WinForms  |  Windows 10 & 11
";
}
