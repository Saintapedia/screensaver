// Services/SettingsManager.cs
// Persists AppSettings as JSON in %AppData%\QuoteScreensaver\settings.json.
using System.Text.Json;
using QuoteScreensaver.Models;

namespace QuoteScreensaver.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> to the user's AppData folder.
/// Falls back to default settings if the file is missing or corrupted.
/// </summary>
public sealed class SettingsManager
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Full path to the settings directory.</summary>
    public string SettingsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "QuoteScreensaver");

    /// <summary>Full path to the settings JSON file.</summary>
    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>Full path to the GitHub quote cache directory.</summary>
    public string CacheDirectory => Path.Combine(SettingsDirectory, "cache");

    /// <summary>Loads settings from disk; returns defaults if file doesn't exist.</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return CreateDefaults();

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            return settings ?? CreateDefaults();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsManager] Load failed: {ex.Message}");
            return CreateDefaults();
        }
    }

    /// <summary>Saves settings to disk. Creates the directory if necessary.</summary>
    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsManager] Save failed: {ex.Message}");
        }
    }

    /// <summary>Ensures both the settings and cache directories exist.</summary>
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }

    private AppSettings CreateDefaults()
    {
        // If no local folder is set, default to the executable's directory.
        return new AppSettings
        {
            LocalQuotesFolder = AppContext.BaseDirectory
        };
    }
}
