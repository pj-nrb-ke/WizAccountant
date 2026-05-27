# Enterprise QA cycle 003 — WizAccountant (app.ascendbooks.biz)
param(
  [string]$BaseUrl = 'https://app.ascendbooks.biz',
  [string]$ApiUrl = 'https://app.ascendbooks.biz'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent
Set-Location $Root

$env:QA_BASE_URL = $BaseUrl
$env:QA_API_URL = $ApiUrl
$env:QA_CYCLE = '003'

Write-Host '=== WizAccountant QA Cycle 003 ===' -ForegroundColor Cyan

Write-Host '>> API: duplicate (25) race (20) session (20)' -ForegroundColor Cyan
node scripts/qa-cycle-003/run-api-suite.mjs
$apiExit = $LASTEXITCODE

Write-Host '>> Playwright: multi-tab (15) long-duration (10) frontend sync (8)' -ForegroundColor Cyan
Push-Location qa
if (-not (Test-Path node_modules)) { npm install }
npx playwright install chromium 2>$null
npx playwright test enterprise-cycle-003.spec.ts --trace retain-on-failure
$pwExit = $LASTEXITCODE
Pop-Location

Write-Host '>> Merge + Excel QA-Test-003.xlsx' -ForegroundColor Cyan
node scripts/merge-qa-cycle-003.mjs
$mergeExit = $LASTEXITCODE
python scripts/generate-qa-003-excel.py

Write-Host '>> Chime' -ForegroundColor Cyan
$mp3 = 'C:\Users\pj\WizFlow\WizFlow-Male.mp3'
if (Test-Path $mp3) {
  Add-Type -AssemblyName presentationCore
  $player = New-Object System.Windows.Media.MediaPlayer
  $player.Open($mp3)
  $player.Play()
  Start-Sleep -Seconds 3
} else {
  [console]::Beep(880, 400)
  [console]::Beep(1100, 400)
}

if ($apiExit -ne 0 -or $pwExit -ne 0) {
  Write-Host "Done with failures api=$apiExit pw=$pwExit. See QA-Test-003.xlsx" -ForegroundColor Yellow
  exit 1
}

Write-Host 'Cycle 003 complete: QA-Test-003.xlsx' -ForegroundColor Green
