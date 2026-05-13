param(
    [string] $Version = "",
    [string] $OutputZip = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Get-Item -LiteralPath $PSScriptRoot).Parent.FullName

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -Raw -LiteralPath (Join-Path $projectRoot "VERSION")).Trim()
}

if ([string]::IsNullOrWhiteSpace($OutputZip)) {
    $OutputZip = Join-Path $projectRoot ("dist\Wither3.access-" + $Version + ".zip")
}

$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Wither3.access-release-" + [System.Guid]::NewGuid().ToString("N"))
$runtimeDir = Join-Path $stagingRoot "Wither3Access"
$toolsDir = Join-Path $stagingRoot "tools"
$vendorDir = Join-Path $runtimeDir "vendor"

function Copy-RequiredItem {
    param(
        [string] $Source,
        [string] $Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Brak wymaganego elementu paczki: $Source"
    }

    $destinationParent = Split-Path -Parent $Destination
    if (-not [string]::IsNullOrWhiteSpace($destinationParent)) {
        New-Item -ItemType Directory -Force -Path $destinationParent | Out-Null
    }

    Copy-Item -LiteralPath $Source -Destination $Destination -Recurse -Force
}

try {
    New-Item -ItemType Directory -Force -Path $runtimeDir, $toolsDir, $vendorDir | Out-Null

    Copy-RequiredItem (Join-Path $projectRoot "README.md") (Join-Path $stagingRoot "README.md")
    Copy-RequiredItem (Join-Path $projectRoot "INSTALL.md") (Join-Path $stagingRoot "INSTALL.md")
    Copy-RequiredItem (Join-Path $projectRoot "CHANGELOG.md") (Join-Path $stagingRoot "CHANGELOG.md")
    Copy-RequiredItem (Join-Path $projectRoot "VERSION") (Join-Path $stagingRoot "VERSION")
    Copy-RequiredItem (Join-Path $projectRoot "Witcher3AccessibleLauncher.exe") (Join-Path $stagingRoot "Witcher3AccessibleLauncher.exe")
    Copy-RequiredItem (Join-Path $projectRoot "Witcher3ScreenReaderBridge.exe") (Join-Path $runtimeDir "Witcher3ScreenReaderBridge.exe")
    Copy-RequiredItem (Join-Path $projectRoot "Witcher3MenuCompanion.exe") (Join-Path $runtimeDir "Witcher3MenuCompanion.exe")
    Copy-RequiredItem (Join-Path $projectRoot "README.md") (Join-Path $runtimeDir "README.Wither3.access.md")
    Copy-RequiredItem (Join-Path $projectRoot "config") (Join-Path $runtimeDir "config")
    Copy-RequiredItem (Join-Path $projectRoot "mods\modWither3Access") (Join-Path $stagingRoot "mods\modWither3Access")
    Copy-RequiredItem (Join-Path $projectRoot "tools\install-release.ps1") (Join-Path $toolsDir "install-release.ps1")

    $nvdaDll = Join-Path $projectRoot "vendor\nvdaControllerClient64.dll"
    if (Test-Path -LiteralPath $nvdaDll -PathType Leaf) {
        Copy-RequiredItem $nvdaDll (Join-Path $vendorDir "nvdaControllerClient64.dll")
    } else {
        Write-Warning "Brak vendor\nvdaControllerClient64.dll; paczka bedzie dzialac przez SAPI, ale bez lokalnej biblioteki NVDA Controller."
    }

    $outputParent = Split-Path -Parent $OutputZip
    New-Item -ItemType Directory -Force -Path $outputParent | Out-Null
    if (Test-Path -LiteralPath $OutputZip -PathType Leaf) {
        Remove-Item -LiteralPath $OutputZip -Force
    }

    Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $OutputZip -CompressionLevel Optimal
    Get-Item -LiteralPath $OutputZip
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}
