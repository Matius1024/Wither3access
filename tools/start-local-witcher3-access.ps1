param(
    [ValidateSet("dx12", "dx11")]
    [string] $Renderer = "dx12",

    [string] $BridgePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$gameDir = "C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3 Wild Hunt"
$launcher = Join-Path $PSScriptRoot "start-witcher3-access.ps1"

$params = @{
    GameDir = $gameDir
    Renderer = $Renderer
}

if ($BridgePath.Trim().Length -gt 0) {
    $params.BridgePath = $BridgePath
}

& $launcher @params
