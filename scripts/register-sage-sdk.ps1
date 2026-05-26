# Unregister then re-register Sage Evolution SDK (32-bit) from the official install folder.
# Run PowerShell as Administrator.
#
#   cd C:\Users\pj\WizAccountant\scripts
#   .\register-sage-sdk.ps1
#
# Optional: custom Evolution path
#   .\register-sage-sdk.ps1 -SageSdkPath "D:\Sage Evolution"

param(
    [string]$SageSdkPath = "${env:ProgramFiles(x86)}\Sage Evolution",
    [switch]$UnregisterOnly
)

$ErrorActionPreference = "Stop"

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    Write-Error "Run this script in PowerShell as Administrator (required for regasm)."
}

if (-not (Test-Path $SageSdkPath)) {
    Write-Error "Sage Evolution folder not found: $SageSdkPath"
}

# Sage SDK is 32-bit — must use 32-bit regasm, not Framework64.
$regasm = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\regasm.exe"
if (-not (Test-Path $regasm)) {
    Write-Error "32-bit regasm.exe not found at: $regasm"
}

$dllNames = @(
    "Pastel.Evolution.dll",
    "Pastel.Evolution.Common.dll"
)

# Paths that may have been registered during development (copied SDK next to our EXE).
$extraProbeRoots = @(
    (Resolve-Path (Join-Path $PSScriptRoot "..\src\WizConnector.Setup\bin\Release\net8.0-windows") -ErrorAction SilentlyContinue),
    (Resolve-Path (Join-Path $PSScriptRoot "..\src\WizConnector.Setup\bin\Debug\net8.0-windows") -ErrorAction SilentlyContinue),
    (Resolve-Path (Join-Path $PSScriptRoot "..\src\WizConnector.Service\bin\Release\net8.0") -ErrorAction SilentlyContinue)
) | Where-Object { $_ -and (Test-Path $_) }

function Invoke-RegAsm {
    param(
        [string]$DllPath,
        [switch]$Unregister
    )

    if (-not (Test-Path $DllPath)) {
        Write-Host "  (skip - not found) $DllPath"
        return
    }

    if ($Unregister) {
        Write-Host "Unregistering: $DllPath"
        & $regasm $DllPath /unregister | Write-Host
    }
    else {
        Write-Host "Registering: $DllPath"
        & $regasm $DllPath /codebase /tlb | Write-Host
    }
}

Write-Host "=== Sage SDK unregister ===" -ForegroundColor Cyan
Write-Host "Official folder: $SageSdkPath"
Write-Host "Using regasm: $regasm"
Write-Host ""

# Unregister copies in build output first, then official install (Evolution before Common is typical).
foreach ($root in $extraProbeRoots) {
    Write-Host "--- Build output: $root ---"
    foreach ($dll in $dllNames) {
        Invoke-RegAsm -DllPath (Join-Path $root $dll) -Unregister
    }
}

Write-Host "--- Official install ---"
foreach ($dll in $dllNames) {
    Invoke-RegAsm -DllPath (Join-Path $SageSdkPath $dll) -Unregister
}

if ($UnregisterOnly) {
    Write-Host ""
    Write-Host "Unregister complete." -ForegroundColor Green
    return
}

Write-Host ""
Write-Host "=== Sage SDK register (official install only) ===" -ForegroundColor Cyan

# Register Common first, then main API (matches typical Sage SDK install order).
Invoke-RegAsm -DllPath (Join-Path $SageSdkPath "Pastel.Evolution.Common.dll")
Invoke-RegAsm -DllPath (Join-Path $SageSdkPath "Pastel.Evolution.dll")

Write-Host ""
Write-Host "Done. SDK registered from:" -ForegroundColor Green
Write-Host "  $SageSdkPath"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Rebuild: dotnet build src\WizConnector.Setup\WizConnector.Setup.csproj -c Release"
Write-Host "  2. Run WizConnector.Setup.exe and Test Sage connection"
Write-Host "  3. Licence serial and key must match your Sage SDK developer licence (Evolution Help / About)"
