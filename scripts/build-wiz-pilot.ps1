# Build WizPilot.exe (manager launcher with buttons)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
dotnet build "$root\src\WizAccountant.Manager\WizAccountant.Manager.csproj" -c Release
$exe = "$root\src\WizAccountant.Manager\bin\Release\net8.0-windows\WizPilot.exe"
if (Test-Path $exe) {
    Write-Host ""
    Write-Host "WizPilot ready:" -ForegroundColor Green
    Write-Host $exe
} else {
    Write-Host "Build failed - exe not found." -ForegroundColor Red
    exit 1
}
