# Point WizAccountant at pj-nrb-ke via SSH (no Git Credential Manager account picker).
# Requires ~/.ssh/config host github.com-pj-nrb-ke and key github_pj_nrb_ke (see New-Agent-Training.md).

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$remote = "git@github.com-pj-nrb-ke:pj-nrb-ke/WizAccountant.git"
git remote set-url origin $remote
git config --local credential.interactive never

Write-Host "origin -> $remote"
ssh -T git@github.com-pj-nrb-ke 2>&1 | ForEach-Object { Write-Host $_ }
git fetch origin
Write-Host "Done. git push/pull will use SSH (account pj-nrb-ke)."
