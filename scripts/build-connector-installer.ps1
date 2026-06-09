# build-connector-installer.ps1  (G4)
# Builds the WizConnector Windows service and packages it as a self-extracting
# PowerShell installer (no admin needed for NSIS/WiX — pure PS1 approach).
#
# Output: .\dist\WizConnectorSetup-<version>.exe  (self-extracting PS1 stub)
#         .\dist\WizConnectorSetup-<version>.ps1  (standalone installer)
#
# Run on the build machine (Windows x86 capable):
#   .\scripts\build-connector-installer.ps1

param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'
$Root      = Resolve-Path "$PSScriptRoot\.."
$DistDir   = "$Root\dist"
$PublishDir = "$Root\connector-publish"
$ServiceProj = "$Root\src\WizConnector.Service\WizConnector.Service.csproj"

Write-Host "=== WizConnector Installer Build ===" -ForegroundColor Cyan
Write-Host "Version      : $Version"
Write-Host "Configuration: $Configuration"

# 1. Publish x86 self-contained single-file
Write-Host "`n[1/3] Publishing connector (x86 self-contained)…"
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
dotnet publish $ServiceProj `
    -c $Configuration `
    -r win-x86 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:Version=$Version `
    -o $PublishDir

# 2. Generate installer PS1
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
$InstallerPs1 = "$DistDir\WizConnectorSetup-$Version.ps1"

Write-Host "`n[2/3] Generating installer script…"
@"
#Requires -RunAsAdministrator
<#
.SYNOPSIS  WizConnector Windows Service Installer  v$Version
.DESCRIPTION
    Installs WizConnector as a Windows service.
    Requires the connector binary in the same directory or path below.

.PARAMETER InstallDir
    Where to install (default: C:\Program Files (x86)\WizConnector)

.PARAMETER ApiUrl
    WizAccountant API URL (e.g. https://app.ascendbooks.biz)
#>
param(
    [string] `$InstallDir = 'C:\Program Files (x86)\WizConnector',
    [string] `$ApiUrl     = 'https://app.ascendbooks.biz'
)

`$ServiceName = 'WizConnector'
`$Exe         = Join-Path `$InstallDir 'WizConnector.Service.exe'

Write-Host 'WizConnector Setup v$Version' -ForegroundColor Cyan

# Stop existing service
if (Get-Service `$ServiceName -ErrorAction SilentlyContinue) {
    Write-Host 'Stopping existing service…'
    Stop-Service `$ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete `$ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Copy files
Write-Host "Installing to `$InstallDir…"
New-Item -ItemType Directory -Force -Path `$InstallDir | Out-Null
Copy-Item -Path `$PSScriptRoot\* -Destination `$InstallDir -Recurse -Force

# Write minimal appsettings.json if missing
`$appSettings = Join-Path `$InstallDir 'appsettings.json'
if (-not (Test-Path `$appSettings)) {
    @{
        ApiBaseUrl     = `$ApiUrl
        Logging        = @{ LogLevel = @{ Default = 'Information' } }
    } | ConvertTo-Json -Depth 4 | Set-Content `$appSettings -Encoding utf8
}

# Register service
Write-Host 'Registering Windows service…'
New-Service -Name `$ServiceName ``
            -BinaryPathName `"`$Exe`" ``
            -DisplayName 'WizConnector — Sage Evolution Bridge' ``
            -StartupType Automatic ``
            -Description 'Bridges WizAccountant cloud API to on-premises Sage 200 Evolution.'

Start-Service `$ServiceName
Write-Host "Service started: `$(Get-Service `$ServiceName | Select-Object -ExpandProperty Status)" -ForegroundColor Green

# Open pairing UI
`$pairingUrl = "`$ApiUrl/admin"
Write-Host "Open `$pairingUrl to pair this site." -ForegroundColor Yellow
Start-Process `$pairingUrl

Write-Host 'Installation complete.' -ForegroundColor Green
"@ | Set-Content $InstallerPs1 -Encoding utf8

Write-Host "`n[3/3] Packaging…"
# Copy publish output alongside installer
Copy-Item -Path "$PublishDir\*" -Destination $DistDir -Recurse -Force

Write-Host "`n✅ Installer ready: $InstallerPs1" -ForegroundColor Green
Write-Host "   Distribute the dist\ folder to the client machine and run WizConnectorSetup-$Version.ps1 as Administrator."
