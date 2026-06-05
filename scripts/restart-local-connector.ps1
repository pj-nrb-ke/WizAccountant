# Stop WizConnector.Service, rebuild pilot apps, restart service (+ tray if built).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

Write-Host "Stopping WizConnector.Service ..." -ForegroundColor Cyan
Get-Process -Name "WizConnector.Service" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  Stop-Process -Id $($_.Id)"
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

Write-Host "Building WizConnector.Service (Release) ..." -ForegroundColor Cyan
dotnet build "$root\src\WizConnector.Service\WizConnector.Service.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Starting WizConnector.Service ..." -ForegroundColor Cyan
Start-Process powershell.exe -ArgumentList @(
    "-NoExit",
    "-Command",
    "`$env:ASPNETCORE_ENVIRONMENT='Development'; Set-Location '$root'; dotnet run --project 'src\WizConnector.Service\WizConnector.Service.csproj' -c Release -- Connector:ApiBaseUrl=http://localhost:5278"
)

$tray = Join-Path $root "src\WizConnector.Tray\bin\Release\net8.0-windows\WizConnector.Tray.exe"
if (Test-Path $tray) {
    Write-Host "Starting WizConnector.Tray ..." -ForegroundColor Cyan
    Start-Process $tray
} else {
    Write-Host "Tray not built. Run: dotnet build src\WizConnector.Tray\WizConnector.Tray.csproj -c Release" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Connector restarted. Retry your Insight query." -ForegroundColor Green
