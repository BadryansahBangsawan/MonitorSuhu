param(
    [ValidateSet("x64", "arm64")]
    [string]$Arch = "x64"
)

$ErrorActionPreference = "Stop"
$windowsRoot = Split-Path $PSScriptRoot -Parent

& "$windowsRoot\publish.ps1" -Arch $Arch

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "Inno Setup 6 not found. Install from https://jrsoftware.org/isinfo.php"
    Write-Host "Published exe is at: $windowsRoot\dist\win-$Arch\MonitorSuhu.exe"
    Write-Host "After installing Inno Setup, re-run: .\installer\build.ps1"
    exit 1
}

& $iscc "$PSScriptRoot\MonitorSuhu.iss"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Installer: $windowsRoot\dist\MonitorSuhu-1.0.0-windows-x64.exe"
