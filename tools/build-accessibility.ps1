Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Get-Item -LiteralPath $PSScriptRoot).Parent.FullName
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "Nie znaleziono kompilatora C#: $compiler"
}

$companionSource = Join-Path $projectRoot "src\Wither3.Access.Companion\Witcher3MenuCompanion.cs"
$bridgeSource = Join-Path $projectRoot "src\Wither3.Access.Bridge\Witcher3ScreenReaderBridge.cs"
$launcherSource = Join-Path $projectRoot "src\Wither3.Access.Launcher\Program.cs"
$companionExe = Join-Path $projectRoot "Witcher3MenuCompanion.exe"
$bridgeExe = Join-Path $projectRoot "Witcher3ScreenReaderBridge.exe"
$launcherExe = Join-Path $projectRoot "Witcher3AccessibleLauncher.exe"
$toolsLauncherExe = Join-Path $PSScriptRoot "Wither3.Access.Launcher.exe"

& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.dll /reference:System.Web.Extensions.dll /out:$companionExe $companionSource
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.dll /out:$bridgeExe $bridgeSource
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.dll /reference:System.Windows.Forms.dll /out:$launcherExe $launcherSource

Copy-Item -LiteralPath $launcherExe -Destination $toolsLauncherExe -Force

Write-Output "Zbudowano:"
Write-Output "  $companionExe"
Write-Output "  $bridgeExe"
Write-Output "  $launcherExe"
Write-Output "  $toolsLauncherExe"
