// Models/AppSettings.cs
// All configurable settings for the screensaver, with serialization-friendly defaults.
using System.Drawing;
using System.Text.Json.Serialization;

namespace QuoteScreensaver.Models;

/// <summary>Quote source mode — which sources to load quotes from.</summary>
public enum QuoteSourceMode { Local, GitHub, Both }

/// <summary>How often the GitHub cache should be refreshed.</summary>
public enum CacheRefreshInterval { Manual, Daily, Weekly, Monthly }

/// <summary>
/// All user-configurable settings. Serialised to/from JSON via SettingsManager.
/// All properties have sensible defaults so the app works out-of-the-box.
/// </summary>
public sealed class AppSettings
{
    // ── Quote Sources ──────────────────────────────────────────────────────────
    public QuoteSourceMode SourceMode { get; set; } = QuoteSourceMode.Both;

    /// <summary>Folder to scan for local .txt / .csv files. Defaults to app folder.</summary>
    public string LocalQuotesFolder { get; set; } = string.Empty;

    /// <summary>GitHub URL (raw file, folder API, or repo tree URL).</summary>
    public string GitHubUrl { get; set; } = string.Empty;

    /// <summary>Comma-separated list of enabled quote set names (empty = all enabled).</summary>
    public string EnabledSets { get; set; } = string.Empty;

    public CacheRefreshInterval CacheRefresh { get; set; } = CacheRefreshInterval.Daily;

    // ── Display ───────────────────────────────────────────────────────────────
    /// <summary>Seconds each quote group is visible before fading out.</summary>
    public float DisplayDurationSeconds { get; set; } = 12f;

    /// <summary>Seconds for a full fade-in or fade-out transition.</summary>
    public float FadeDurationSeconds { get; set; } = 1.5f;

    /// <summary>Maximum number of quotes visible on screen at once (1–3).</summary>
    public int MaxQuotesOnScreen { get; set; } = 2;

    /// <summary>Movement speed multiplier (1 = default ~40 px/s).</summary>
    public float SpeedMultiplier { get; set; } = 1.0f;

    public bool ShowAuthor { get; set; } = true;
    public bool EnableCollisions { get; set; } = true;
    public bool ShowTextShadow { get; set; } = true;

    // ── Appearance ────────────────────────────────────────────────────────────
    /// <summary>Background color stored as ARGB int for JSON serialization.</summary>
    public int BackgroundColorArgb { get; set; } = Color.FromArgb(255, 10, 10, 20).ToArgb();

    /// <summary>Text color stored as ARGB int.</summary>
    public int TextColorArgb { get; set; } = Color.FromArgb(255, 230, 220, 200).ToArgb();

    /// <summary>Optional font family override. Empty = use "Segoe UI".</summary>
    public string FontFamily { get; set; } = "Segoe UI";

    // ── Computed colour properties (not serialized) ───────────────────────────
    [JsonIgnore]
    public Color BackgroundColor
    {
        get => Color.FromArgb(BackgroundColorArgb);
        set => BackgroundColorArgb = value.ToArgb();
    }

    [JsonIgnore]
    public Color TextColor
    {
        get => Color.FromArgb(TextColorArgb);
        set => TextColorArgb = value.ToArgb();
    }

    // ── Preset GitHub URLs ────────────────────────────────────────────────────
    /// <summary>Built-in example GitHub raw-file URLs for popular quote sources.</summary>
    [JsonIgnore]
    public static readonly IReadOnlyList<(string Label, string Url)> GitHubPresets = new[]
    {
        ("tumblr-famous-quotes (txt)",  "https://raw.githubusercontent.com/borisgloger/famous-quotes/master/quotes.txt"),
        ("awesomequotes4u (csv)",        "https://raw.githubusercontent.com/public-apis/public-apis/master/README.md"),
        ("Stoic quotes (txt)",           "https://raw.githubusercontent.com/shortthirdman/stoic-quotes/main/stoic-quotes.txt"),
        ("Motivational Quotes (csv)",    "https://raw.githubusercontent.com/akhiltak/inspirational-quotes/master/Quotes.csv"),
        ("Custom URL…",                  ""),
    };
}
