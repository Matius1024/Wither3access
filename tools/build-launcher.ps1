Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Get-Item -LiteralPath $PSScriptRoot).Parent.FullName
$source = Join-Path $projectRoot "src\Wither3.Access.Launcher\Program.cs"
$output = Join-Path $PSScriptRoot "Wither3.Access.Launcher.exe"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "Nie znaleziono kompilatora C#: $compiler"
}

& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.dll /reference:System.Windows.Forms.dll /out:$output $source

if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
    throw "Kompilacja nie utworzyla pliku: $output"
}

Write-Output $output
