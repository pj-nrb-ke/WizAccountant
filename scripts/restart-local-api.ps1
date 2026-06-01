# Stops whatever is listening on the local API port, then starts a fresh WizAccountant.Api.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$port = 5278

Write-Host "Stopping listeners on port $port ..." -ForegroundColor Cyan
$conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
$pids = $conns | Select-Object -ExpandProperty OwningProcess -Unique
foreach ($procId in $pids) {
    if ($procId -gt 0) {
        Write-Host "  Stop-Process -Id $procId"
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    }
}
Start-Sleep -Seconds 1

Write-Host "Starting WizAccountant.Api on http://localhost:$port ..." -ForegroundColor Cyan
Start-Process powershell.exe -ArgumentList @(
    "-NoExit",
    "-Command",
    "Set-Location '$root'; dotnet run --project 'src\WizAccountant.Api\WizAccountant.Api.csproj' --launch-profile http"
)

Write-Host ""
Write-Host "Wait for 'Now listening on: http://localhost:$port'" -ForegroundColor Yellow
Write-Host "Check version:  Invoke-RestMethod http://localhost:$port/health | ConvertTo-Json" -ForegroundColor Yellow
Write-Host "Open Insight:   http://localhost:$port/insight/" -ForegroundColor Green
