# Quote Screensaver

[![GitHub release](https://img.shields.io/github/v/release/Saintapedia/screensaver)](https://github.com/Saintapedia/screensaver/releases)
[![Windows 10/11](https://img.shields.io/badge/Windows-10%20%7C%2011-blue?logo=windows)](https://github.com/Saintapedia/screensaver/releases)
[![.NET 8 bundled](https://img.shields.io/badge/.NET%208-bundled%20(no%20install%20needed)-purple?logo=dotnet)](https://github.com/Saintapedia/screensaver/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

An elegant, animated Windows screensaver built with C# and .NET 8 WinForms.

> **Zero prerequisites** — the .NET 8 runtime is bundled into the single `.scr` file.
> Just download, copy, and go.

Quotes float and bounce around the screen, smoothly fading in and out. Load from local files, GitHub repositories, or both.

---

## 🚀 Quick Install (No build required)

> **Requires Windows 10 (1809+) or Windows 11. No .NET installation needed.**

### Option A — One-line PowerShell (recommended)

Open PowerShell **as Administrator** and run:

```powershell
irm https://raw.githubusercontent.com/Saintapedia/screensaver/main/install.ps1 | iex
```

### Option B — Manual

1. Go to [**Releases**](https://github.com/Saintapedia/screensaver/releases) and download `QuoteScreensaver.scr`
2. Copy it to `C:\Windows\System32\` (requires Administrator)
3. Right-click the `.scr` → **Install**
   *or* open **Settings → Personalization → Lock Screen → Screen saver**
4. Select **Quote Screensaver** and click **Settings...** to configure

---

## ✨ Features

| Feature | Details |
|---|---|
| **No prerequisites** | .NET 8 runtime bundled — works on any Windows 10/11 machine |
| **Smooth 60 FPS animation** | Double-buffered rendering, GDI+ accelerated |
| **Bouncing physics** | Edge bounce + inter-sprite elastic collision |
| **Smooth fades** | Configurable fade in/out; never a hard cut |
| **Dynamic font sizing** | Short quotes = big font; long quotes = smaller font |
| **1–3 simultaneous quotes** | Configurable |
| **Local files** | `.txt` (one per line) and `.csv` (Quote,Author) |
| **GitHub integration** | Raw URL, folder listing via GitHub Contents API |
| **Offline cache** | Cached in `%AppData%\QuoteScreensaver\cache\` |
| **Multi-monitor** | One window per screen, all in sync |
| **Per-monitor DPI** | Manifest declares `PerMonitorV2` awareness |
| **Keyboard shortcuts** | Space / Arrows / P / R / A / H / Esc while running |
| **Settings dialog** | Tabbed GUI — no config file editing required |

---

## ⌨️ Keyboard Shortcuts

| Key | Action |
|---|---|
| `Space` / `→` | Next quote |
| `←` | Previous quote |
| `P` | Pause / Resume |
| `R` | Reload quotes from all sources |
| `A` | Toggle author attribution |
| `H` | Show / hide keyboard shortcut overlay |
| `Esc` / any key / mouse move >4 px | Exit screensaver |

---

## 🗂 Project Structure

```
QuoteScreensaver/
├── QuoteScreensaver.csproj      # .NET 8 WinForms, self-contained single-file
├── app.manifest                 # PerMonitorV2 DPI + Windows 10/11 compat
├── build-and-install.ps1        # Developer build script (auto-installs .NET SDK)
├── install.ps1                  # End-user installer (downloads latest release)
├── Program.cs                   # Entry point: /s /p /c arg routing
│
├── Models/
│   ├── Quote.cs                 # Quote { Text, Author, SourceName }
│   ├── QuoteSet.cs              # Named collection from one source file
│   └── AppSettings.cs          # All settings (JSON-serializable + typed defaults)
│
├── Services/
│   ├── SettingsManager.cs      # Load/save %AppData%\QuoteScreensaver\settings.json
│   ├── LocalQuoteLoader.cs     # .txt (one-per-line) + .csv (Quote,Author) parser
│   ├── GitHubQuoteLoader.cs    # Download, cache, URL normalisation, fallback
│   └── QuoteSetManager.cs      # Coordinate sources, shuffle-bag, history nav
│
├── Animation/
│   ├── FadeState.cs            # Hidden | FadingIn | Visible | FadingOut
│   ├── QuoteSprite.cs          # Physics, fade, auto font sizing, shadow rendering
│   └── AnimationEngine.cs      # 60 FPS loop, transitions, keyboard actions, help HUD
│
├── Forms/
│   ├── ScreensaverForm.cs      # Full-screen multi-monitor form (/s and /p preview)
│   └── SettingsForm.cs         # 4-tab settings dialog (/c)
│
└── Resources/
    ├── sample_quotes.txt       # 20 quotes — plain text format
    └── sample_quotes.csv       # 20 quotes — CSV format
```

---

## 🔨 Build from Source

The `build-and-install.ps1` script **automatically installs the .NET 8 SDK** if it isn't
already present, then produces a fully self-contained single `.scr` file.

```powershell
# Clone
git clone https://github.com/Saintapedia/screensaver.git
cd QuoteScreensaver

# Build only (~80 MB self-contained output, .NET bundled)
.\build-and-install.ps1

# Build + install to System32 (run PowerShell as Administrator)
.\build-and-install.ps1 -Install
```

The script uses the official Microsoft `dotnet-install.ps1` to install the SDK to
`%LOCALAPPDATA%\Microsoft\dotnet` — no Administrator rights needed just for the SDK.

### Manual build (if you already have .NET 8 SDK)

```powershell
dotnet publish -r win-x64 -c Release
# Output: bin\Release\net8.0-windows\win-x64\publish\QuoteScreensaver.exe
# Rename to .scr and copy to System32
```

---

## 📁 Local Quote Files

Place `.txt` or `.csv` files in any folder, then point the **Sources** tab at it.

### `.txt` format
```
# Lines starting with # are comments
The only way to do great work is to love what you do.
"Life is short." — Unknown
A longer quote — Author Name
```

### `.csv` format
```csv
Quote,Author
"Be the change","Mahatma Gandhi"
"To be or not to be","Shakespeare"
```

---

## 🐙 GitHub Integration

| URL Format | Example |
|---|---|
| **Raw file** | `https://raw.githubusercontent.com/user/repo/main/quotes.txt` |
| **Blob (auto-converted)** | `https://github.com/user/repo/blob/main/quotes.txt` |
| **Tree folder** | `https://github.com/user/repo/tree/main/quotes/` |
| **Contents API** | `https://api.github.com/repos/user/repo/contents/quotes/` |

Quotes are cached in `%AppData%\QuoteScreensaver\cache\` with a configurable refresh interval.

### Popular public quote repositories to try

- `https://raw.githubusercontent.com/akhiltak/inspirational-quotes/master/Quotes.csv`
- `https://raw.githubusercontent.com/shortthirdman/stoic-quotes/main/stoic-quotes.txt`

---

## 🛠 Troubleshooting

| Problem | Fix |
|---|---|
| Screensaver not in Settings | Ensure `.scr` is in `C:\Windows\System32\` |
| Nothing showing | Check Sources tab; built-in quotes always present as fallback |
| GitHub not loading | Use the Test button; check firewall / proxy |
| Exits immediately | Ignore first mouse event — subsequent moves > 4 px exit |
| High CPU | Reduce Max Quotes to 1, increase Display Duration |

---

## 📄 License

MIT — free to use, modify, and distribute. See [LICENSE](LICENSE).
