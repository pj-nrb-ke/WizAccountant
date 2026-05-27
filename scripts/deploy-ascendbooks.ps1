# Deploy WizAccountant to app.ascendbooks.biz (git push + SSH deploy).
# Requires: config/secrets/ascendbooks-server.credentials.txt (see .example file)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$credFile = Join-Path $root "config\secrets\ascendbooks-server.credentials.txt"

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

if (-not (Test-Path $credFile)) {
    Write-Host "Missing credentials file."
    Write-Host "Copy config\secrets\ascendbooks-server.credentials.example.txt"
    Write-Host "  to config\secrets\ascendbooks-server.credentials.txt"
    Write-Host "Add root password or SSH key, then run this script again."
    exit 1
}

$cfg = Read-Credentials $credFile
$hostName = if ($cfg["HOST"]) { $cfg["HOST"] } else { "167.86.125.230" }
$user = if ($cfg["USER"]) { $cfg["USER"] } else { "root" }
$repo = if ($cfg["GITHUB_REPO"]) { $cfg["GITHUB_REPO"] } else { "git@github.com:pj-nrb-ke/pj-nrb-ke/WizAccountant.git" }

Write-Host "==> Git push (local)"
Push-Location $root
git push origin main
if ($LASTEXITCODE -ne 0) { throw "git push failed" }

Write-Host "==> Bootstrap /opt/wizaccountant on server (if needed)"
$bootstrap = @"
set -eu
if [ ! -d /opt/wizaccountant/.git ]; then
  apt-get update -qq
  apt-get install -y -qq git docker.io docker-compose-plugin 2>/dev/null || apt-get install -y -qq git docker.io
  systemctl enable --now docker 2>/dev/null || true
  git clone '$repo' /opt/wizaccountant || git clone 'https://github.com/pj-nrb-ke/WizAccountant.git' /opt/wizaccountant
fi
"@
Invoke-RemoteCommand $hostName $user $bootstrap $cfg

Write-Host "==> Deploy on server"
Invoke-RemoteCommand $hostName $user "cd /opt/wizaccountant && git fetch origin && git reset --hard origin/main && bash scripts/deploy-vps-wizaccountant.sh" $cfg

Write-Host "==> Done. Open https://app.ascendbooks.biz/health"
