# Weekly pilot review — triage export + stabilization checklist (SAGE-OPS-001).
# Requires local API on http://localhost:5278 (or pass -BaseUrl).
param(
    [string]$BaseUrl = "http://localhost:5278",
    [string]$TenantId = "pilot-tenant",
    [int]$Days = 7
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$exportScript = Join-Path $scriptDir "export-insight-triage.ps1"

Write-Host ""
Write-Host "=== Sage AI Pilot — Weekly Review ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $exportScript)) {
    Write-Error "Missing $exportScript"
}

& $exportScript -BaseUrl $BaseUrl -TenantId $TenantId -Days $Days

Write-Host ""
Write-Host "=== Checklist (see DOCS/Pilot_Stabilization_Workflow.md) ===" -ForegroundColor Yellow
Write-Host "  [ ] Classify failures using DOCS/Query_Triage_Priority.md"
Write-Host "  [ ] Add new real queries to DOCS/Real_Insight_Queries.md"
Write-Host "  [ ] Update DOCS/Capability_Gap_Register.md for new gaps"
Write-Host "  [ ] Promote 3-10 approved cases from candidates/ to tests/intents/"
Write-Host "  [ ] Fix Critical + High items only this week"
Write-Host "  [ ] dotnet test tests/WizAccountant.Insight.Intents.Tests"
Write-Host "  [ ] Update DOCS/Pilot_Query_Signoff.md for validated queries"
Write-Host "  [ ] Deploy with InsightChatInfo version bump if routes changed"
Write-Host ""
