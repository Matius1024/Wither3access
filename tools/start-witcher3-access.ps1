param(
    [Parameter(Mandatory = $true)]
    [string] $GameDir,

    [ValidateSet("dx12", "dx11")]
    [string] $Renderer = "dx12",

    [string] $BridgePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedGameDir = (Resolve-Path -LiteralPath $GameDir).Path

if ($Renderer -eq "dx12") {
    $relativeExe = "bin\x64_dx12\witcher3.exe"
} else {
    $relativeExe = "bin\x64\witcher3.exe"
}

$gameExe = Join-Path $resolvedGameDir $relativeExe
if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf)) {
    throw "Nie znaleziono pliku gry: $gameExe"
}

if ($BridgePath.Trim().Length -gt 0) {
    $resolvedBridge = (Resolve-Path -LiteralPath $BridgePath).Path
    if (-not (Test-Path -LiteralPath $resolvedBridge -PathType Leaf)) {
        throw "Nie znaleziono bridge: $resolvedBridge"
    }

    Start-Process -FilePath $resolvedBridge -WorkingDirectory (Split-Path -LiteralPath $resolvedBridge -Parent)
}

Start-Process -FilePath $gameExe -WorkingDirectory (Split-Path -LiteralPath $gameExe -Parent)
