# Export Insight triage report and candidate regression tests (self-training loop).
# Requires local API on http://localhost:5278
param(
    [string]$BaseUrl = "http://localhost:5278",
    [string]$TenantId = "pilot-tenant",
    [int]$Days = 7,
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $root = Split-Path $PSScriptRoot -Parent
    $OutDir = Join-Path $root "tests\intents\candidates"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$uri = "$BaseUrl/api/insight/triage?tenantId=$TenantId&days=$Days"
Write-Host "GET $uri" -ForegroundColor Cyan
$resp = Invoke-RestMethod -Uri $uri -Method Get

$stamp = Get-Date -Format "yyyy-MM-dd"
$mdPath = Join-Path $OutDir "triage-report-$stamp.md"
$jsonPath = Join-Path $OutDir "candidate-tests-$stamp.json"

$resp.markdown | Set-Content -Path $mdPath -Encoding utf8
$resp.candidateTestsJson | Set-Content -Path $jsonPath -Encoding utf8

Write-Host "Wrote:" -ForegroundColor Green
Write-Host "  $mdPath"
Write-Host "  $jsonPath"
Write-Host ""
Write-Host "Review candidate JSON, move approved cases to tests/intents/, then run dotnet test." -ForegroundColor Yellow
