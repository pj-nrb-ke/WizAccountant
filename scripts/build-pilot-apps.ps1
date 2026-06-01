# Build connector pilot apps only (Service, Tray, Setup) — not WizPilot itself.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

$projects = @(
    "$root\src\WizConnector.Service\WizConnector.Service.csproj",
    "$root\src\WizConnector.Tray\WizConnector.Tray.csproj",
    "$root\src\WizConnector.Setup\WizConnector.Setup.csproj"
)

Write-Host "Building pilot apps (Service, Tray, Setup)..." -ForegroundColor Cyan
Write-Host ""

foreach ($proj in $projects) {
    Write-Host ">> dotnet build $proj -c Release" -ForegroundColor Yellow
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Build FAILED." -ForegroundColor Red
        Read-Host "Press Enter to close"
        exit 1
    }
    Write-Host ""
}

Write-Host "Build complete. You can close this window and return to WizPilot." -ForegroundColor Green
Read-Host "Press Enter to close"
