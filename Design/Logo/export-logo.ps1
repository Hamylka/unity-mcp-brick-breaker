# Renders the final logo (logo-final.js) to PNG files with headless Microsoft Edge.
# Game assets go to Assets/Art/Logo, store and press images to Design/Logo/export.
# Usage (from the repository root): powershell -ExecutionPolicy Bypass -File Design/Logo/export-logo.ps1

$ErrorActionPreference = 'Stop'
$edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$page = 'file:///' + ((Join-Path $PSScriptRoot 'logo-export.html') -replace '\\', '/' -replace ' ', '%20')
$game = Join-Path $root 'Assets\Art\Logo'
$store = Join-Path $PSScriptRoot 'export'
New-Item -ItemType Directory -Force $game, $store | Out-Null

# asset id, output file, width, height
$jobs = @(
    @('icon',  "$game\Icon_App.png",                1024, 1024),
    @('fg',    "$game\Icon_Adaptive_Foreground.png", 432,  432),
    @('bg',    "$game\Icon_Adaptive_Background.png", 432,  432),
    @('wordt', "$game\Logo_Wordmark.png",           2400, 1200),
    @('icon',  "$store\Icon_Store_512.png",          512,  512),
    @('word',  "$store\Logo_KeyArt.png",            2560, 1280)
)
$ErrorActionPreference = 'Continue' # Edge writes harmless log lines to stderr
foreach ($j in $jobs) {
    & $edge --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files `
        --default-background-color=00000000 --force-device-scale-factor=1 `
        --window-size="$($j[2]),$($j[3])" --virtual-time-budget=5000 `
        --screenshot="$($j[1])" "$page`?a=$($j[0])" 2>$null | Out-Null
    Write-Output "$($j[1]) ($($j[2])x$($j[3]))"
}
