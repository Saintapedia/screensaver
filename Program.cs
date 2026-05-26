// Program.cs
// Entry point: parse /s, /p, /c arguments and launch the correct form.
using System.Threading;
using System.Windows.Forms;
using QuoteScreensaver.Forms;
using QuoteScreensaver.Models;
using QuoteScreensaver.Services;

namespace QuoteScreensaver;

static class Program
{
    // Global mutex name — prevents multiple screensaver instances.
    private const string MutexName = "Global\\QuoteScreensaverSingleInstance";

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // ── Parse command-line argument ────────────────────────────────────────
        // Windows screensaver host passes:
        //   /s            Show screensaver
        //   /p <hwnd>     Preview in the given window handle
        //   /c            Configure (settings dialog)
        //   (nothing)     Default — show settings
        //
        // Some callers use dash instead of slash, or combine /s with hwnd.

        string mode    = args.Length > 0 ? args[0].ToLowerInvariant().Trim() : "/c";
        string? hwndArg = args.Length > 1 ? args[1].Trim() : null;

        // Handle "/s:12345" shorthand used by some hosts
        if ((mode.StartsWith("/s:") || mode.StartsWith("-s:")) && mode.Length > 3)
        {
            hwndArg = mode[3..];
            mode    = "/p";
        }

        // Normalise dashes to slashes
        if (mode.StartsWith('-')) mode = '/' + mode[1..];

        switch (mode)
        {
            case "/s":
                RunScreensaver();
                break;

            case "/p":
                nint hwnd = 0;
                if (hwndArg != null) nint.TryParse(hwndArg, out hwnd);
                RunPreview(hwnd);
                break;

            case "/c":
            default:
                RunSettings();
                break;
        }
    }

    // ── /s — Full screensaver ─────────────────────────────────────────────────

    private static void RunScreensaver()
    {
        // Prevent duplicate screensaver processes
        using var mutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance) return;

        var (settings, settingsManager) = LoadSettings();
        var quoteManager = new QuoteSetManager(settings, settingsManager);

        var screens = Screen.AllScreens;

        // Create one ScreensaverForm per physical monitor
        var forms = new ScreensaverForm[screens.Length];
        for (int i = 0; i < screens.Length; i++)
        {
            bool isPrimary = screens[i].Primary;
            forms[i] = new ScreensaverForm(screens[i], settings, quoteManager,
                                            isPrimary: isPrimary);
        }

        // Show secondary monitors first (they are not the Application.Run target)
        for (int i = 0; i < screens.Length; i++)
        {
            if (!screens[i].Primary)
            {
                forms[i].Show();
                forms[i].TopMost = true;
            }
        }

        // When the primary form closes, close all others
        int primaryIdx = Array.FindIndex(screens, s => s.Primary);
        if (primaryIdx < 0) primaryIdx = 0;

        forms[primaryIdx].FormClosed += (_, _) =>
        {
            foreach (var f in forms)
                if (f is { IsDisposed: false } && f != forms[primaryIdx])
                    f.Close();
        };

        Application.Run(forms[primaryIdx]);
    }

    // ── /p — Preview mode ─────────────────────────────────────────────────────

    private static void RunPreview(nint previewHwnd)
    {
        var (settings, settingsManager) = LoadSettings();
        var quoteManager = new QuoteSetManager(settings, settingsManager);

        using var form = new ScreensaverForm(
            screen: null,
            settings: settings,
            quoteManager: quoteManager,
            isPrimary: true,
            previewHwnd: previewHwnd);

        Application.Run(form);
    }

    // ── /c — Settings dialog ──────────────────────────────────────────────────

    private static void RunSettings()
    {
        var (settings, settingsManager) = LoadSettings();
        var quoteManager = new QuoteSetManager(settings, settingsManager);
        quoteManager.InitializeSync(); // need sets loaded for the set-picker list

        using var form = new SettingsForm(settings, settingsManager, quoteManager);
        Application.Run(form);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (AppSettings settings, SettingsManager manager) LoadSettings()
    {
        var manager  = new SettingsManager();
        manager.EnsureDirectoriesExist();
        var settings = manager.Load();
        return (settings, manager);
    }
}
