# Start API + Connector Service + Tray for local Phase 1 dev.
# Run from repo root: .\scripts\start-wizconnector-local.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

Write-Host "Starting WizAccountant.Api on http://localhost:5278 ..."
Start-Process powershell -ArgumentList @(
    "-NoExit", "-Command",
    "cd '$root'; dotnet run --project src\WizAccountant.Api --launch-profile http"
)

Start-Sleep -Seconds 4

Write-Host "Starting WizConnector.Service ..."
Start-Process powershell -ArgumentList @(
    "-NoExit", "-Command",
    "`$env:ASPNETCORE_ENVIRONMENT='Development'; cd '$root'; dotnet run --project src\WizConnector.Service -c Release -- Connector:ApiBaseUrl=http://localhost:5278"
)

$tray = Join-Path $root "src\WizConnector.Tray\bin\Release\net8.0-windows\WizConnector.Tray.exe"
if (Test-Path $tray) {
    Write-Host "Starting WizConnector.Tray ..."
    Start-Process $tray
}
else {
    Write-Host "Tray not built yet. Run: dotnet build src\WizConnector.Tray\WizConnector.Tray.csproj -c Release"
}

Write-Host ""
Write-Host "Admin UI: http://localhost:5278/admin/"
Write-Host "Look for WizConnector icon in the system tray (near the clock)."
