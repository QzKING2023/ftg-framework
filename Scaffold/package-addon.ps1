# FTG Framework — Addon packaging script (Windows PowerShell)
# Usage: .\Scaffold\package-addon.ps1 [-Version x.y.z]
# Output: Scaffold\ftg-framework-<version>.zip (default version read from plugin.cfg)
# Zip layout: top-level ftg-framework/ folder — extract into the project's addons/ directory.

param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

$addonDir = Join-Path $repoRoot "addons\ftg-framework"
$pluginCfg = Join-Path $addonDir "plugin.cfg"
$srcDir = Join-Path $addonDir "src"
$outputDir = Join-Path $repoRoot "Scaffold"

if (-not $Version) {
    $versionLine = Select-String -Path $pluginCfg -Pattern '^version="(.+)"' | Select-Object -First 1
    if (-not $versionLine) { throw "Could not read version from $pluginCfg" }
    $Version = $versionLine.Matches[0].Groups[1].Value
}

$zipName = "ftg-framework-$Version.zip"
$zipPath = Join-Path $outputDir $zipName

Write-Host "Packaging FTG Framework $Version..."

# Clean and recreate src directory
if (Test-Path $srcDir) { Remove-Item -Recurse -Force $srcDir }
New-Item -ItemType Directory -Force -Path $srcDir | Out-Null

# Shared module manifest — single source of truth for CLI and packagers
$manifestPath = Join-Path $repoRoot "Scaffold\framework-source-dirs.txt"
if (-not (Test-Path $manifestPath)) { throw "Manifest not found: $manifestPath" }
$sourceDirs = Get-Content $manifestPath |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -and -not $_.StartsWith('#') }
if (-not $sourceDirs) { throw "Manifest is empty: $manifestPath" }

foreach ($dir in $sourceDirs) {
    $srcPath = Join-Path $repoRoot ($dir -replace '/', '\')
    if (-not (Test-Path $srcPath)) { throw "Framework source directory missing: $srcPath" }

    # Addon layout strips the Scripts/Framework/ prefix
    $rel = ($dir -replace '^Scripts/Framework/?', '') -replace '/', '\'
    $dstPath = Join-Path $srcDir $rel

    Get-ChildItem -Path $srcPath -Recurse -File |
        Where-Object { $_.Name -notlike '*.uid' } |
        ForEach-Object {
        $relFile = $_.FullName.Substring($srcPath.Length).TrimStart('\')
        $parts = $relFile -split '\\'
        if ($parts | Where-Object { $_ -in @('bin', 'obj', '.godot') }) { return }
        # Top-level GameLoop.cs ships separately as GameLoop.cs.template
        if ($parts.Count -eq 1 -and $_.Name -eq 'GameLoop.cs') { return }

        $dest = Join-Path $dstPath $relFile
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
        Copy-Item $_.FullName -Destination $dest -Force
        Write-Host "  COPY $rel\$relFile"
    }
}

# Copy FrameRateManager.cs
$frmSrc = Join-Path $repoRoot "Scripts\FrameRateManager.cs"
if (-not (Test-Path $frmSrc)) { throw "FrameRateManager.cs not found: $frmSrc" }
Copy-Item $frmSrc -Destination $srcDir -Force
Write-Host "  COPY FrameRateManager.cs"

# Copy GameLoop as a template with a non-.cs extension so host projects don't compile it
$glSrc = Join-Path $repoRoot "Scripts\Framework\Core\GameLoop.cs"
if (-not (Test-Path $glSrc)) { throw "GameLoop.cs not found: $glSrc" }
$glDst = Join-Path $srcDir "Core\GameLoop.cs.template"
Copy-Item $glSrc -Destination $glDst -Force
Write-Host "  COPY Core\GameLoop.cs.template"

# Bundle the repo LICENSE if one exists (required for Asset Library submission)
$licenseSrc = Join-Path $repoRoot "LICENSE"
if (Test-Path $licenseSrc) {
    Copy-Item $licenseSrc -Destination $addonDir -Force
    Write-Host "  COPY LICENSE"
} else {
    Write-Host "  WARN no LICENSE at repo root — Asset Library submission requires one"
}

# Stage outside the Godot-watched addon tree. Godot may create .uid sidecars
# concurrently while the solution tests run; a private staging tree makes the
# final inventory deterministic and lets us remove every generated sidecar.
$packageRoot = Join-Path $outputDir ".ftg-package-$PID"
$packageAddon = Join-Path $packageRoot "ftg-framework"
try {
    New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
    Copy-Item -Path $addonDir -Destination $packageAddon -Recurse -Force
    Get-ChildItem -Path $packageAddon -Recurse -File -Filter '*.uid' | Remove-Item -Force
    $packageCfg = Join-Path $packageAddon "plugin.cfg"
    $packageCfgContent = (Get-Content -Raw $packageCfg) -replace 'script="[^"]+"', 'script="src/Editor/FTGEditorPlugin.cs"'
    Set-Content -Path $packageCfg -Value $packageCfgContent -NoNewline -Encoding utf8
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path $packageAddon -DestinationPath $zipPath -Force
} finally {
    if (Test-Path $packageRoot) { Remove-Item -Path $packageRoot -Recurse -Force }
}

Write-Host ""
Write-Host "Addon packaged: $zipPath"
Write-Host "Done."
