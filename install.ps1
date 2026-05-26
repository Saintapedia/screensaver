#Requires -Version 5.1
<#
.SYNOPSIS
    Downloads and installs the latest Quote Screensaver release from GitHub.

.DESCRIPTION
    • Downloads QuoteScreensaver.scr from the latest GitHub Release.
    • NO .NET installation required (the runtime is bundled).
    • Copies to C:\Windows\System32\ (requires Administrator).
    • Opens Screen Saver Settings when done.

.PARAMETER Uninstall
    Removes QuoteScreensaver.scr from System32.

.EXAMPLE
    # Install (run as Administrator)
    .\install.ps1

    # Uninstall
    .\install.ps1 -Uninstall
#>
param([switch]$Uninstall)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoOwner = "Saintapedia"
$RepoName  = "QuoteScreensaver"
$ScrName   = "QuoteScreensaver.scr"
$System32  = "$env:SystemRoot\System32"
$DestScr   = Join-Path $System32 $ScrName

# ── Helper functions ──────────────────────────────────────────────────────────
function Require-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p  = [Security.Principal.WindowsPrincipal] $id
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host ""
        Write-Host "  ERROR: This script must be run as Administrator." -ForegroundColor Red
        Write-Host ""
        Write-Host "  Right-click PowerShell → 'Run as Administrator', then re-run:" -ForegroundColor Yellow
        Write-Host "  .\install.ps1" -ForegroundColor White
        Write-Host ""
        exit 1
    }
}

function Write-Banner {
    param([string]$T, [string]$C = 'Cyan')
    Write-Host ""
    Write-Host ("═" * 50) -ForegroundColor $C
    Write-Host "  $T" -ForegroundColor $C
    Write-Host ("═" * 50) -ForegroundColor $C
    Write-Host ""
}

# ── Uninstall path ─────────────────────────────────────────────────────────────
if ($Uninstall) {
    Require-Admin
    Write-Banner "Uninstalling Quote Screensaver"
    if (Test-Path $DestScr) {
        Remove-Item $DestScr -Force
        Write-Host "  ✔  Removed $DestScr" -ForegroundColor Green
    } else {
        Write-Host "  Not installed (nothing to remove)." -ForegroundColor Gray
    }
    Write-Host ""
    exit 0
}

# ── Install path ───────────────────────────────────────────────────────────────
Require-Admin
Write-Banner "Quote Screensaver — Installer"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# 1. Fetch latest release info
Write-Host "[1/3] Looking up latest release on GitHub..." -ForegroundColor Yellow
$apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
try {
    $release = Invoke-RestMethod -Uri $apiUrl -Headers @{ 'User-Agent' = 'QuoteScreensaverInstaller' }
} catch {
    Write-Host ""
    Write-Host "  ERROR: Could not reach GitHub API." -ForegroundColor Red
    Write-Host "  Check your internet connection or visit:" -ForegroundColor Yellow
    Write-Host "  https://github.com/$RepoOwner/$RepoName/releases" -ForegroundColor White
    Write-Host ""
    exit 1
}

$asset = $release.assets | Where-Object { $_.name -eq $ScrName } | Select-Object -First 1
if (-not $asset) {
    Write-Host "  ERROR: $ScrName not found in latest release assets." -ForegroundColor Red
    Write-Host "  Available assets: $($release.assets.name -join ', ')" -ForegroundColor Gray
    exit 1
}

Write-Host "      ✔  Found release: $($release.tag_name)" -ForegroundColor Green

# 2. Download
$tmpScr = Join-Path $env:TEMP $ScrName
Write-Host "[2/3] Downloading $ScrName ($([math]::Round($asset.size/1MB,1)) MB)..." -ForegroundColor Yellow

try {
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tmpScr -UseBasicParsing
    Write-Host "      ✔  Download complete." -ForegroundColor Green
} catch {
    Write-Host "  ERROR: Download failed: $_" -ForegroundColor Red
    exit 1
}

# 3. Copy to System32
Write-Host "[3/3] Installing to System32..." -ForegroundColor Yellow
Copy-Item -Path $tmpScr -Destination $DestScr -Force
Remove-Item $tmpScr -Force -ErrorAction SilentlyContinue
Write-Host "      ✔  Installed to $DestScr" -ForegroundColor Green

# Done
Write-Banner "Installation Complete!" "Green"
Write-Host "  Quote Screensaver is now installed!" -ForegroundColor White
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor Cyan
Write-Host "    1. Open Screen Saver Settings:" -ForegroundColor White
Write-Host "       Settings → Personalization → Lock Screen → Screen saver" -ForegroundColor Gray
Write-Host "    2. Select 'Quote Screensaver' from the dropdown." -ForegroundColor White
Write-Host "    3. Click 'Settings...' to add your own quotes or GitHub sources." -ForegroundColor White
Write-Host ""

# Offer to open Screen Saver Settings
$open = Read-Host "Open Screen Saver Settings now? [Y/n]"
if ($open -ne 'n' -and $open -ne 'N') {
    Start-Process "rundll32.exe" "shell32.dll,Control_RunDLL desk.cpl,,1"
}
