# Prepare local pilot for testing (build + optional API restart).
# Run from repo root:  .\scripts\prepare-local-test.ps1
# Close WizPilot first if WizPilot.exe build fails (file lock).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

Write-Host "==> Intent regression tests" -ForegroundColor Cyan
dotnet test tests\WizAccountant.Insight.Intents.Tests\WizAccountant.Insight.Intents.Tests.csproj --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Connector (Release)" -ForegroundColor Cyan
dotnet build src\WizConnector.Service\WizConnector.Service.csproj -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> API (Debug)" -ForegroundColor Cyan
dotnet build src\WizAccountant.Api\WizAccountant.Api.csproj -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "API build failed (file locked?). Close WizPilot + API console, then re-run." -ForegroundColor Yellow
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Ready. Next:" -ForegroundColor Green
Write-Host "  1. WizPilot -> Restart local API" -ForegroundColor White
Write-Host "  2. WizPilot -> Start service + tray" -ForegroundColor White
Write-Host "  3. http://localhost:5278/insight/  (Ctrl+F5)" -ForegroundColor White
Write-Host "  4. See DOCS\TEST-CONSOLIDATION-LOCAL.md" -ForegroundColor White
Write-Host ""
$restart = Read-Host "Restart local API now? (y/N)"
if ($restart -eq "y") {
    & "$root\scripts\restart-local-api.ps1"
}
