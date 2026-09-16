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
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $out "MonitorSuhu.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Publish succeeded but $exe was not produced."
    exit 1
}

Write-Host "Published: $exe"
