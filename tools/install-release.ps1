param(
    [string] $GameDir = "C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3 Wild Hunt"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Get-Item -LiteralPath $PSScriptRoot).Parent.FullName
$dx12Exe = Join-Path $GameDir "bin\x64_dx12\witcher3.exe"
$dx11Exe = Join-Path $GameDir "bin\x64\witcher3.exe"

if (-not (Test-Path -LiteralPath $dx12Exe -PathType Leaf) -and -not (Test-Path -LiteralPath $dx11Exe -PathType Leaf)) {
    throw "Nie znaleziono witcher3.exe w: $GameDir"
}

$sourceMod = Join-Path $projectRoot "mods\modWither3Access"
$sourceRuntime = Join-Path $projectRoot "Wither3Access"
$sourceBridge = Join-Path $sourceRuntime "Witcher3ScreenReaderBridge.exe"
$sourceCompanion = Join-Path $sourceRuntime "Witcher3MenuCompanion.exe"
$sourceConfig = Join-Path $sourceRuntime "config"
$sourceVendor = Join-Path $sourceRuntime "vendor\nvdaControllerClient64.dll"

if (-not (Test-Path -LiteralPath $sourceBridge -PathType Leaf)) {
    $sourceBridge = Join-Path $projectRoot "Witcher3ScreenReaderBridge.exe"
}
if (-not (Test-Path -LiteralPath $sourceCompanion -PathType Leaf)) {
    $sourceCompanion = Join-Path $projectRoot "Witcher3MenuCompanion.exe"
}
if (-not (Test-Path -LiteralPath $sourceConfig -PathType Container)) {
    $sourceConfig = Join-Path $projectRoot "config"
}
if (-not (Test-Path -LiteralPath $sourceVendor -PathType Leaf)) {
    $sourceVendor = Join-Path $projectRoot "vendor\nvdaControllerClient64.dll"
}

$targetMods = Join-Path $GameDir "mods"
$targetRuntime = Join-Path $GameDir "Wither3Access"
$targetVendor = Join-Path $targetRuntime "vendor"
$targetConfig = Join-Path $targetRuntime "config"

if (-not (Test-Path -LiteralPath $sourceMod -PathType Container)) {
    throw "Nie znaleziono folderu moda: $sourceMod"
}

New-Item -ItemType Directory -Force -Path $targetMods | Out-Null
New-Item -ItemType Directory -Force -Path $targetRuntime | Out-Null
New-Item -ItemType Directory -Force -Path $targetVendor | Out-Null

Copy-Item -LiteralPath $sourceMod -Destination $targetMods -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "Witcher3AccessibleLauncher.exe") -Destination (Join-Path $GameDir "Witcher3AccessibleLauncher.exe") -Force
Copy-Item -LiteralPath $sourceBridge -Destination (Join-Path $targetRuntime "Witcher3ScreenReaderBridge.exe") -Force
Copy-Item -LiteralPath $sourceCompanion -Destination (Join-Path $targetRuntime "Witcher3MenuCompanion.exe") -Force
Copy-Item -LiteralPath $sourceConfig -Destination $targetRuntime -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination (Join-Path $targetRuntime "README.Wither3.access.md") -Force

if (Test-Path -LiteralPath $sourceVendor -PathType Leaf) {
    Copy-Item -LiteralPath $sourceVendor -Destination (Join-Path $targetVendor "nvdaControllerClient64.dll") -Force
}

Write-Output "Zainstalowano Wither3.access 0.3.alfa:"
Write-Output "  Launcher: $(Join-Path $GameDir "Witcher3AccessibleLauncher.exe")"
Write-Output "  Runtime:  $targetRuntime"
Write-Output "  Mod:      $(Join-Path $targetMods "modWither3Access")"
