param(
    [ValidateSet("x64", "arm64")]
    [string]$Arch = "x64"
)

$ErrorActionPreference = "Stop"
$rid = "win-$Arch"
$out = Join-Path $PSScriptRoot "dist\$rid"

dotnet publish "$PSScriptRoot\src\MonitorSuhu.App\MonitorSuhu.App.csproj" `
    -c Release `
    -r $rid `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out

Write-Host "Published: $out\MonitorSuhu.exe"
