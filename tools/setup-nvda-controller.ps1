param(
    [string] $Source
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Get-Item -LiteralPath $PSScriptRoot).Parent.FullName
$vendorDir = Join-Path $projectRoot "vendor"
$target = Join-Path $vendorDir "nvdaControllerClient64.dll"

function Find-NvdaControllerClient64 {
    if ($Source) {
        if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
            throw "Nie znaleziono wskazanego pliku: $Source"
        }
        return (Resolve-Path -LiteralPath $Source).Path
    }

    $knownPaths = @(
        "C:\Program Files\NVDA\nvdaControllerClient64.dll",
        "C:\Program Files (x86)\Steam\steamapps\common\Hades II\Ship\nvdaControllerClient64.dll",
        "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\stardew-access\lib\screen-reader-libs\windows\nvdaControllerClient64.dll",
        "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\RimWorldAccess\nvdaControllerClient64.dll"
    )

    foreach ($path in $knownPaths) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            return $path
        }
    }

    $found = Get-ChildItem -Path "C:\Program Files", "C:\Program Files (x86)" -Recurse -Filter "nvdaControllerClient64.dll" -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($found) {
        return $found.FullName
    }

    return $null
}

$sourcePath = Find-NvdaControllerClient64
if (-not $sourcePath) {
    throw "Nie znaleziono nvdaControllerClient64.dll. Podaj sciezke parametrem -Source."
}

New-Item -ItemType Directory -Force -Path $vendorDir | Out-Null
Copy-Item -LiteralPath $sourcePath -Destination $target -Force

Write-Output "Skopiowano z: $sourcePath"
Write-Output "Skopiowano do: $target"
