#requires -Version 5
[CmdletBinding()]
param(
    [string]$Output = "",
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $root "dist\DeskOrder.exe"
}
$outDir = Split-Path -Parent $Output
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force $outDir | Out-Null }

$icon = Join-Path $root "assets\app.ico"
$manifest = Join-Path $root "src\app.manifest"
if (-not (Test-Path $icon)) {
    $py = Get-Command python -ErrorAction SilentlyContinue
    if ($py) { & $py.Source (Join-Path $root "tools\make_icon.py") | Out-Null }
    elseif (Test-Path (Join-Path $root "tools\make_icon.py")) {
        throw "assets\app.ico missing; install Python 3 and run: python tools\make_icon.py"
    }
    else { throw "assets\app.ico missing" }
    if (-not (Test-Path $icon)) { throw "tools\make_icon.py did not produce assets\app.ico" }
}

$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Xml.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.Web.Extensions.dll",
    "System.Runtime.Serialization.dll",
    "Microsoft.CSharp.dll",
    "System.Xaml.dll",
    "WPF\PresentationFramework.dll",
    "WPF\PresentationCore.dll",
    "WPF\WindowsBase.dll"
) | ForEach-Object { Join-Path $fw $_ } | Where-Object { Test-Path $_ }

$sources = Get-ChildItem (Join-Path $root "src") -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
if (-not $sources) { throw "No source files found." }

$args = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/debug-",
    "/langversion:5",
    "/codepage:65001",
    "/nowarn:1591,0618",
    "/win32icon:$icon",
    "/win32manifest:$manifest",
    "/out:$Output"
)
foreach ($r in $refs) { $args += "/r:$r" }
$args += $sources

if (-not $Quiet) { Write-Host "Compiling $($sources.Count) files -> $Output" }
$log = & $csc @args 2>&1
if ($LASTEXITCODE -ne 0) {
    $log | ForEach-Object { Write-Host $_ }
    throw "Build failed with exit code $LASTEXITCODE"
}
if ($log) { $log | ForEach-Object { Write-Host $_ } }
if (-not (Test-Path $Output)) { throw "Build reported success but output is missing." }
if (-not $Quiet) {
    $item = Get-Item $Output
    Write-Host ("OK  {0}  ({1:N0} bytes)" -f $item.Name, $item.Length)
}
