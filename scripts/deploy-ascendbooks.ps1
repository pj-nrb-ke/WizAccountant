# Deploy WizAccountant to app.ascendbooks.biz — GitHub only (no local file upload).
# 1) git push origin main from this PC
# 2) SSH to VPS: git fetch + reset --hard origin/main + docker build on server

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$credFile = Join-Path $root "config\secrets\ascendbooks-server.credentials.txt"
$defaultKey = Join-Path $env:USERPROFILE ".ssh\contabo_wizerp"

function Read-Credentials([string]$path) {
    $cfg = @{}
    Get-Content $path | ForEach-Object {
        $line = $_.Trim()
        if ($line -eq "" -or $line.StartsWith("#")) { return }
        $i = $line.IndexOf("=")
        if ($i -gt 0) {
            $cfg[$line.Substring(0, $i).Trim()] = $line.Substring($i + 1).Trim()
        }
    }
    return $cfg
}

function Invoke-RemoteCommand([string]$hostName, [string]$user, [string]$command, [hashtable]$cfg) {
    $target = "${user}@${hostName}"
    if ($cfg["SSH_PRIVATE_KEY"]) {
        $key = $cfg["SSH_PRIVATE_KEY"]
        if (-not (Test-Path $key)) { throw "SSH key not found: $key" }
        ssh -o StrictHostKeyChecking=accept-new -i $key $target $command
        return
    }
    if ($cfg["PASSWORD"]) {
        $plink = Get-Command plink -ErrorAction SilentlyContinue
        if ($plink) {
            & plink -batch -pw $cfg["PASSWORD"] $target $command
            return
        }
        $wsl = Get-Command wsl -ErrorAction SilentlyContinue
        if ($wsl) {
            $pass = $cfg["PASSWORD"] -replace "'", "'\''"
            wsl bash -lc "command -v sshpass >/dev/null && SSHPASS='$pass' sshpass -e ssh -o StrictHostKeyChecking=no $target '$command'"
            if ($LASTEXITCODE -eq 0) { return }
        }
        throw "PASSWORD set but plink/sshpass unavailable. Install PuTTY plink or WSL+sshpass, or set SSH_PRIVATE_KEY in credentials file."
    }
    throw "Set PASSWORD or SSH_PRIVATE_KEY in $credFile"
}

if (Test-Path $credFile) {
    $cfg = Read-Credentials $credFile
} else {
    $cfg = @{}
}
if (-not $cfg["SSH_PRIVATE_KEY"] -and (Test-Path $defaultKey)) {
    $cfg["SSH_PRIVATE_KEY"] = $defaultKey
}
if (-not $cfg["SSH_PRIVATE_KEY"] -and -not $cfg["PASSWORD"]) {
    Write-Host "No SSH access. Use $defaultKey (see config/secrets/website-hosting-notes.md)"
    Write-Host "  or create config\secrets\ascendbooks-server.credentials.txt"
    exit 1
}

$hostName = if ($cfg["HOST"]) { $cfg["HOST"] } else { "167.86.125.230" }
$user = if ($cfg["USER"]) { $cfg["USER"] } else { "root" }

Write-Host "==> Git push (local)"
Push-Location $root
git push origin main
if ($LASTEXITCODE -ne 0) { throw "git push failed" }

Write-Host "==> Deploy on server (git pull from GitHub only)"
$remoteDeploy = @'
set -eu
APP=/opt/wizaccountant
if [ ! -d "$APP/.git" ]; then
  echo "First-time: clone on server: git clone git@github.com:pj-nrb-ke/WizAccountant.git $APP"
  exit 1
fi
cd "$APP"
git fetch origin
git checkout main
git reset --hard origin/main
bash scripts/deploy-vps-wizaccountant.sh
'@
Invoke-RemoteCommand $hostName $user $remoteDeploy $cfg

Write-Host "==> Done. Open https://app.ascendbooks.biz/health"
