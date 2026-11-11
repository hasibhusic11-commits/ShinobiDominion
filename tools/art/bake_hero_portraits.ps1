# Bake character portraits from the project's real Heroes3D prefabs.
# Run from any directory on a Windows machine with Unity Hub / Unity 6 installed.
# Usage: powershell -ExecutionPolicy Bypass -File tools/art/bake_hero_portraits.ps1
[CmdletBinding()]
param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe",
    [string]$ProjectPath = "",
    [switch]$SkipBatch
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repo "unity"
}
if (-not (Test-Path (Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"))) {
    throw "Not a Unity project: $ProjectPath"
}
if (-not (Test-Path $UnityExe)) {
    throw "Unity 6000.3.28f1 not found at $UnityExe. Supply -UnityExe with your installed editor path."
}

$logs = Join-Path $repo "artifacts"
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$log = Join-Path $logs "hero-portrait-bake.log"

if ($SkipBatch) {
    Write-Host "Open Unity and run: Tools > Shinobi Dominion > Art > Bake Missing Hero Portraits"
    exit 0
}

$args = @(
    "-batchmode", "-quit",
    "-projectPath", $ProjectPath,
    "-logFile", $log,
    "-executeMethod", "ShinobiDominion.Editor.HeroPortraitBaker.BakeForCI"
)
Write-Host "Baking the game's own 3D hero models into Resources/HeroPortraits..."
& $UnityExe @args
$code = $LASTEXITCODE
if ($code -ne 0) {
    if (Test-Path $log) { Get-Content $log -Tail 50 }
    throw "Unity portrait bake failed with exit code $code. Review $log."
}

Get-Content $log -Tail 25
Write-Host "Complete. Review Assets/_Game/Resources/HeroPortraits/ and check the Heroes Altar in PlayMode."
Write-Host "Commit generated PNG and .meta files only after visually confirming the portraits."
