# install-k6.ps1  — Downloads k6 portable binary to .\tools\k6.exe
# Run once before executing load tests.
# No admin required — downloads user-local.

$k6Version = "v0.54.0"
$k6Url     = "https://github.com/grafana/k6/releases/download/$k6Version/k6-$k6Version-windows-amd64.zip"
$dest      = Join-Path $PSScriptRoot "..\tools\k6"
$zip       = Join-Path $env:TEMP "k6.zip"

if (Test-Path "$dest\k6.exe") {
    Write-Host "k6 already installed at $dest\k6.exe" -ForegroundColor Green
    & "$dest\k6.exe" version
    exit 0
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null
Write-Host "Downloading k6 $k6Version…"
Invoke-WebRequest -Uri $k6Url -OutFile $zip -UseBasicParsing

Write-Host "Extracting…"
Expand-Archive -Path $zip -DestinationPath $dest -Force
# k6 extracts into a subdirectory — flatten
$inner = Get-ChildItem $dest -Directory | Select-Object -First 1
if ($inner) {
    Move-Item "$($inner.FullName)\k6.exe" $dest
    Remove-Item $inner.FullName -Recurse -Force
}
Remove-Item $zip

Write-Host "k6 installed at $dest\k6.exe" -ForegroundColor Green
& "$dest\k6.exe" version
