# P1-29: second pilot site validation (AR/AP read handlers via run-wait API).
# Use when validating a second Evolution company/version, or re-run against BlankVer11 as site #2.

param(
    [string]$ApiBaseUrl = "http://localhost:5278",
    [string]$TenantId = "pilot-tenant",
    [string]$SiteName = "Pilot Site 2",
    [string]$EvolutionVersionNote = "BlankVer11 / Sage Evolution 11 (pilot #2)"
)

$ErrorActionPreference = "Stop"

Write-Host "=== P1-29 Pilot site #2 ===" -ForegroundColor Cyan
Write-Host "Evolution: $EvolutionVersionNote"

Invoke-RestMethod "$ApiBaseUrl/health" | Out-Null
Write-Host "API OK"

$pairBody = @{ tenantId = $TenantId; siteName = $SiteName; expiresInMinutes = 30 } | ConvertTo-Json
$pair = Invoke-RestMethod -Uri "$ApiBaseUrl/api/pairing-codes" -Method Post -ContentType "application/json" -Body $pairBody
Write-Host "Pairing code: $($pair.pairingCode)"

Write-Host @"

Start connector with this pairing code, then press Enter.
  Connector:PairingCode=$($pair.pairingCode)
  Connector:DeviceId=PILOT-SITE2-01

"@
Read-Host "Press Enter when site shows Online in admin"

$sites = Invoke-RestMethod "$ApiBaseUrl/api/sites"
$site = $sites | Where-Object { $_.siteName -eq $SiteName -and $_.isOnline } | Select-Object -First 1
if (-not $site) { $site = $sites | Where-Object { $_.isOnline } | Select-Object -First 1 }
if (-not $site) { throw "No online site found." }

Write-Host "Using site $($site.siteId) ($($site.siteName))" -ForegroundColor Green

$ops = @(
    @{ operation = "site.health"; parameters = @{} },
    @{ operation = "customer.list"; parameters = @{ criteria = "DCLink > 0" } },
    @{ operation = "customertransaction.list"; parameters = @{ criteria = "AutoIdx > 0" } },
    @{ operation = "supplier.list"; parameters = @{ criteria = "DCLink > 0" } },
    @{ operation = "suppliertransaction.list"; parameters = @{ criteria = "AutoIdx > 0" } }
)

foreach ($op in $ops) {
    Write-Host "`n--- $($op.operation) ---" -ForegroundColor Cyan
    $body = @{
        siteId = $site.siteId
        operation = $op.operation
        parameters = $op.parameters
        requestedBy = "pilot-site2-script"
        timeoutSeconds = 90
    } | ConvertTo-Json -Depth 5
    $result = Invoke-RestMethod -Uri "$ApiBaseUrl/api/jobs/run-wait" -Method Post -ContentType "application/json" -Body $body
    if ($result.status -eq "Failed" -or $result.status -eq 3) {
        Write-Host "FAILED: $($result.error)" -ForegroundColor Red
    } else {
        Write-Host "OK" -ForegroundColor Green
        if ($result.resultJson) { $result.resultJson.Substring(0, [Math]::Min(400, $result.resultJson.Length)) }
    }
}

Write-Host "`nP1-29 script finished." -ForegroundColor Green
