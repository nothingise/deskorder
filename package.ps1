#requires -Version 5
<#
    Builds DeskOrder and stages a portable release folder + zip that can be
    sent to other people.

    release\
      DeskOrder-1.0.0-portable\        (what the user sees after unzipping)
      DeskOrder-1.0.0-portable.zip

    The script itself is pure ASCII on purpose: Windows PowerShell 5.1 reads
    .ps1 files as ANSI unless they carry a UTF-8 BOM, so the two Chinese file
    names are built from code points instead of string literals.
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$OutDir = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root "release" }

# 桌序.exe  /  使用说明.txt
$cnExe = ([string][char]0x684C) + ([string][char]0x5E8F) + ".exe"
$cnGuide = ([string][char]0x4F7F) + ([string][char]0x7528) + ([string][char]0x8BF4) + ([string][char]0x660E) + ".txt"
$cnProcess = (([string][char]0x684C) + ([string][char]0x5E8F))

$name = "DeskOrder-$Version-portable"
$stage = Join-Path $OutDir $name
$zip = Join-Path $OutDir "$name.zip"

if (-not $SkipBuild) {
    Get-Process | Where-Object { $_.ProcessName -eq "DeskOrder" -or $_.ProcessName -eq $cnProcess } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
    & (Join-Path $root "build.ps1") -Quiet
}

$exe = Join-Path $root "dist\DeskOrder.exe"
if (-not (Test-Path $exe)) { throw "missing build output: $exe" }
$guide = Join-Path $root ("docs\" + $cnGuide)
if (-not (Test-Path $guide)) { throw "missing user guide: $guide" }

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

Copy-Item $exe (Join-Path $stage "DeskOrder.exe") -Force
Copy-Item $exe (Join-Path $stage $cnExe) -Force

# Re-encode the docs as UTF-8 *with* BOM: the repo copies are BOM-less, and some
# Windows editors (older Notepad, WordPad) then show Chinese as mojibake.
function Copy-TextWithBom([string]$source, [string]$target) {
    $text = [System.IO.File]::ReadAllText($source, (New-Object System.Text.UTF8Encoding($false)))
    [System.IO.File]::WriteAllText($target, $text, (New-Object System.Text.UTF8Encoding($true)))
}
Copy-TextWithBom (Join-Path $root "README.md") (Join-Path $stage "README.md")
Copy-TextWithBom $guide (Join-Path $stage $cnGuide)

$portable = @(
    "Portable mode for DeskOrder",
    "",
    "Keep this file next to DeskOrder.exe and all settings, Dock pins,",
    "playlists and logs stay in the DeskOrderData folder beside the program",
    "instead of %APPDATA%\DeskOrder.",
    "",
    "Delete this file to store data in %APPDATA%\DeskOrder again."
)
Set-Content -Path (Join-Path $stage "portable.txt") -Value $portable -Encoding UTF8

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
$sums = @(
    "SHA256",
    "DeskOrder.exe  $hash",
    "$cnExe         $hash",
    "",
    "verify> certutil -hashfile DeskOrder.exe SHA256"
)
Set-Content -Path (Join-Path $stage "SHA256.txt") -Value $sums -Encoding UTF8

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $zip) { Remove-Item $zip -Force }
# No encoding argument on purpose: .NET then writes non-ASCII names as UTF-8 AND
# sets the UTF-8 flag (bit 11), which Explorer/7-Zip/WinRAR all honour. Passing
# UTF8Encoding explicitly writes the same bytes but leaves the flag clear, and
# every extractor then shows the name as mojibake.
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $stage, $zip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true)

$staged = Get-ChildItem $stage -Recurse -File
Write-Host ("staged {0} files -> {1}" -f $staged.Count, $stage)
foreach ($f in $staged) { Write-Host ("  {0,-16} {1,10:N0} bytes" -f $f.Name, $f.Length) }
Write-Host ("zip    {0}  ({1:N0} bytes)" -f $zip, (Get-Item $zip).Length)
