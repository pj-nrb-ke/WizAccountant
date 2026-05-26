# P1-28 end-to-end pilot: pairing code -> connector -> Customer.List job
# Prerequisites: API running on http://localhost:5278, Sage config saved, SDK registered.

param(
    [string]$ApiBaseUrl = "http://localhost:5278",
    [string]$TenantId = "pilot-tenant",
    [string]$SiteName = "BlankVer11 Pilot"
)

$ErrorActionPreference = "Stop"

Write-Host "=== 1. Health ===" -ForegroundColor Cyan
Invoke-RestMethod "$ApiBaseUrl/health" | Out-Null
Write-Host "API OK"

Write-Host "=== 2. Pairing code ===" -ForegroundColor Cyan
$pairBody = @{ tenantId = $TenantId; siteName = $SiteName; expiresInMinutes = 30 } | ConvertTo-Json
$pair = Invoke-RestMethod -Uri "$ApiBaseUrl/api/pairing-codes" -Method Post -ContentType "application/json" -Body $pairBody
Write-Host "Pairing code: $($pair.pairingCode) (expires $($pair.expiresAtUtc))"

Write-Host "=== 3. Start connector (separate window) ===" -ForegroundColor Cyan
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Write-Host @"

  cd $repoRoot
  `$env:ASPNETCORE_ENVIRONMENT = 'Development'
  dotnet run --project src\WizConnector.Service\WizConnector.Service.csproj -c Release -- `
    Connector:PairingCode=$($pair.pairingCode) `
    Connector:ApiBaseUrl=$ApiBaseUrl `
    Connector:DeviceId=PILOT-PC-01

Wait for: Connected to hub for SiteId=...
"@
Read-Host "Press Enter when connector is connected"

Write-Host "=== 4. Sites ===" -ForegroundColor Cyan
$sites = Invoke-RestMethod "$ApiBaseUrl/api/sites"
$sites | Format-Table siteId, siteName, isOnline, lastSeenUtc
$site = $sites | Select-Object -First 1
if (-not $site) { throw "No sites paired." }
if (-not $site.isOnline) { Write-Warning "Site not online yet; waiting 5s..."; Start-Sleep 5; $sites = Invoke-RestMethod "$ApiBaseUrl/api/sites"; $site = $sites | Select-Object -First 1 }

Write-Host "=== 5. Customer.List job ===" -ForegroundColor Cyan
$jobBody = @{
    siteId = $site.siteId
    operation = "customer.list"
    parameters = @{}
    requestedBy = "pilot-script"
} | ConvertTo-Json
$job = Invoke-RestMethod -Uri "$ApiBaseUrl/api/jobs" -Method Post -ContentType "application/json" -Body $jobBody
Write-Host "JobId: $($job.jobId) Status: $($job.status)"

for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    $j = Invoke-RestMethod "$ApiBaseUrl/api/jobs/$($job.jobId)"
    if ($j.status -eq "Completed" -or $j.status -eq 2) {
        Write-Host "SUCCESS" -ForegroundColor Green
        $j.resultJson
        break
    }
    if ($j.status -eq "Failed" -or $j.status -eq 3) {
        Write-Host "FAILED: $($j.error)" -ForegroundColor Red
        break
    }
    Write-Host "  ... $($j.status)"
}
