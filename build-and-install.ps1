#Requires -Version 5.1
<#
.SYNOPSIS
    Bootstraps the .NET 8 SDK if missing, builds Quote Screensaver as a
    fully self-contained single .exe, and optionally installs it to System32.

.DESCRIPTION
    End-users who ONLY want to install the screensaver do NOT need to run
    this script — just grab the pre-built QuoteScreensaver.scr from the
    GitHub Releases page and follow the installation instructions in README.md.

    Run this script if you want to BUILD from source.

    Steps performed:
      1. Detect .NET 8 SDK.  If missing → download & install silently.
      2. dotnet publish (self-contained, single .exe, win-x64).
      3. Rename output .exe → .scr.
      4. (Optional -Install) Copy .scr to System32 and register.

.PARAMETER Install
    Also installs to C:\Windows\System32\. Requires Administrator.

.PARAMETER NoPublish
    Skip the build step (use a previously built binary).

.PARAMETER DotNetInstallDir
    Where to install the .NET 8 SDK if not already present.
    Defaults to "$env:LOCALAPPDATA\Microsoft\dotnet" (no Admin required).

.EXAMPLE
    # Build only
    .\build-and-install.ps1

    # Build and install (run as Administrator)
    .\build-and-install.ps1 -Install

    # Re-install without rebuilding
    .\build-and-install.ps1 -Install -NoPublish

    # Install SDK to a custom location, then build + install
    .\build-and-install.ps1 -Install -DotNetInstallDir "D:\dotnet"
#>
param(
    [switch]$Install,
    [switch]$NoPublish,
    [string]$DotNetInstallDir = "$env:LOCALAPPDATA\Microsoft\dotnet"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir = $PSScriptRoot
$PublishDir = Join-Path $ProjectDir "bin\Release\net8.0-windows\win-x64\publish"
$ExePath    = Join-Path $PublishDir "QuoteScreensaver.exe"
$ScrPath    = Join-Path $PublishDir "QuoteScreensaver.scr"
$System32   = "$env:SystemRoot\System32"
$DestScr    = Join-Path $System32 "QuoteScreensaver.scr"

# ─────────────────────────────────────────────────────────────────────────────
function Write-Banner {
    param([string]$Text, [string]$Color = 'Cyan')
    Write-Host ""
    Write-Host ("═" * 55) -ForegroundColor $Color
    Write-Host "  $Text" -ForegroundColor $Color
    Write-Host ("═" * 55) -ForegroundColor $Color
    Write-Host ""
}

function Write-Step {
    param([int]$N, [int]$Total, [string]$Text)
    Write-Host "[$N/$Total] $Text" -ForegroundColor Yellow
}

function Write-OK   { param([string]$T) Write-Host "      ✔  $T" -ForegroundColor Green }
function Write-Fail { param([string]$T) Write-Host "  ✖  $T" -ForegroundColor Red }
# ─────────────────────────────────────────────────────────────────────────────

Write-Banner "Quote Screensaver — Build & Install Script"

$totalSteps = if ($Install) { 4 } else { 3 }

# ── Step 1: Detect / Install .NET 8 SDK ──────────────────────────────────────
Write-Step 1 $totalSteps "Checking for .NET 8 SDK..."

function Find-DotNet {
    # 1. Try PATH
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) {
        try {
            $v = & dotnet --version 2>&1
            if ($LASTEXITCODE -eq 0 -and ($v -match '^8\.')) { return $cmd.Source }
        } catch {}
    }

    # 2. Try our custom install dir
    $customDotNet = Join-Path $DotNetInstallDir "dotnet.exe"
    if (Test-Path $customDotNet) {
        try {
            $v = & $customDotNet --version 2>&1
            if ($LASTEXITCODE -eq 0 -and ($v -match '^8\.')) { return $customDotNet }
        } catch {}
    }

    # 3. Try Program Files
    $pfDotNet = "$env:ProgramFiles\dotnet\dotnet.exe"
    if (Test-Path $pfDotNet) {
        try {
            $v = & $pfDotNet --version 2>&1
            if ($LASTEXITCODE -eq 0 -and ($v -match '^8\.')) { return $pfDotNet }
        } catch {}
    }

    return $null
}

$dotnetExe = Find-DotNet

if (-not $dotnetExe) {
    Write-Host "      .NET 8 SDK not found. Installing automatically..." -ForegroundColor Yellow
    Write-Host "      Install directory: $DotNetInstallDir" -ForegroundColor Gray
    Write-Host ""

    # Download the official Microsoft .NET install script
    $installScriptUrl  = "https://dot.net/v1/dotnet-install.ps1"
    $installScriptPath = Join-Path $env:TEMP "dotnet-install.ps1"

    Write-Host "      Downloading dotnet-install.ps1 from Microsoft..." -ForegroundColor Gray
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $installScriptUrl -OutFile $installScriptPath -UseBasicParsing
    } catch {
        Write-Host ""
        Write-Fail "Failed to download the .NET installer: $_"
        Write-Host ""
        Write-Host "  Please install .NET 8 SDK manually:" -ForegroundColor Yellow
        Write-Host "  https://dotnet.microsoft.com/download/dotnet/8" -ForegroundColor White
        exit 1
    }

    Write-Host "      Running dotnet-install.ps1 (Channel 8.0, SDK)..." -ForegroundColor Gray
    Write-Host "      This may take a few minutes depending on your connection." -ForegroundColor Gray
    Write-Host ""

    try {
        & $installScriptPath -Channel 8.0 -InstallDir $DotNetInstallDir -Runtime "dotnet" -Verbose:$false
        # Also install the SDK (not just runtime) so we can build
        & $installScriptPath -Channel 8.0 -InstallDir $DotNetInstallDir -Verbose:$false
    } catch {
        Write-Host ""
        Write-Fail ".NET installation failed: $_"
        Write-Host "  Please install manually: https://dotnet.microsoft.com/download/dotnet/8" -ForegroundColor Yellow
        exit 1
    }

    # Add to PATH for this session
    $env:PATH = "$DotNetInstallDir;$env:PATH"

    # Verify
    $dotnetExe = Find-DotNet
    if (-not $dotnetExe) {
        Write-Fail "Installation appeared to succeed but dotnet.exe cannot be found."
        exit 1
    }

    Write-OK ".NET 8 SDK installed to $DotNetInstallDir"
    Write-Host ""
    Write-Host "      ℹ  To use dotnet from a new terminal, add this to your PATH:" -ForegroundColor Cyan
    Write-Host "         $DotNetInstallDir" -ForegroundColor White
    Write-Host ""
} else {
    $sdkVer = & $dotnetExe --version 2>&1
    Write-OK ".NET SDK $sdkVer found at $dotnetExe"
}

# ── Step 2: Build ─────────────────────────────────────────────────────────────
if (-not $NoPublish) {
    Write-Step 2 $totalSteps "Building self-contained release (win-x64)..."
    Write-Host "      This bundles the .NET 8 runtime into the .exe." -ForegroundColor Gray
    Write-Host "      Output will be ~80 MB — no prerequisites for end users." -ForegroundColor Gray
    Write-Host ""

    Push-Location $ProjectDir
    try {
        & $dotnetExe publish `
            -r win-x64 `
            -c Release `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:PublishReadyToRun=true `
            --nologo `
            -v minimal

        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)" }
        Write-OK "Build succeeded."
    } catch {
        Write-Fail "Build failed: $_"
        Pop-Location
        exit 1
    } finally {
        Pop-Location
    }
} else {
    Write-Step 2 $totalSteps "Skipping build (-NoPublish specified)."
}

# ── Step 3: Rename .exe → .scr ───────────────────────────────────────────────
Write-Step 3 $totalSteps "Renaming output .exe to .scr..."

if (-not (Test-Path $ExePath)) {
    Write-Fail "Expected build output not found: $ExePath"
    Write-Host "  Run without -NoPublish to build first." -ForegroundColor Yellow
    exit 1
}

Copy-Item -Path $ExePath -Destination $ScrPath -Force
Write-OK "Created: $ScrPath"
$sizeMB = [math]::Round((Get-Item $ScrPath).Length / 1MB, 1)
Write-Host "      File size: ${sizeMB} MB (runtime bundled — zero prerequisites)" -ForegroundColor Gray

# ── Step 4: Install to System32 ──────────────────────────────────────────────
if ($Install) {
    Write-Step 4 $totalSteps "Installing to System32..."

    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal] $identity

    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host ""
        Write-Fail "Administrator rights are required to install to System32."
        Write-Host ""
        Write-Host "  Option A — Re-run this script as Administrator:" -ForegroundColor Yellow
        Write-Host "    Right-click PowerShell → 'Run as Administrator'" -ForegroundColor White
        Write-Host "    .\build-and-install.ps1 -Install -NoPublish" -ForegroundColor White
        Write-Host ""
        Write-Host "  Option B — Manual installation:" -ForegroundColor Yellow
        Write-Host "    copy `"$ScrPath`" `"$DestScr`"" -ForegroundColor White
        Write-Host "    Then right-click the .scr → Install" -ForegroundColor White
        Write-Host ""
        exit 1
    }

    Copy-Item -Path $ScrPath -Destination $DestScr -Force
    Write-OK "Installed to: $DestScr"

    Write-Banner "Installation Complete!" "Green"
    Write-Host "  Open Screen Saver Settings:" -ForegroundColor Cyan
    Write-Host "    Settings → Personalization → Lock Screen → Screen saver" -ForegroundColor White
    Write-Host "  Select 'Quote Screensaver' and click Settings... to configure." -ForegroundColor White
    Write-Host ""
    Write-Host "  Or right-click the .scr file → Install" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host ""
    Write-Banner "Build Complete!" "Green"
    Write-Host "  Output: $ScrPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  To test without installing:" -ForegroundColor Yellow
    Write-Host "    & `"$ScrPath`" /c    # Settings dialog" -ForegroundColor White
    Write-Host "    & `"$ScrPath`" /s    # Full screensaver" -ForegroundColor White
    Write-Host ""
    Write-Host "  To install (run PowerShell as Administrator):" -ForegroundColor Yellow
    Write-Host "    .\build-and-install.ps1 -Install -NoPublish" -ForegroundColor White
    Write-Host ""
}
