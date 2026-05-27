# Deploy to app.ascendbooks.biz

Production host: **167.86.125.230** (`app.ascendbooks.biz`).  
This is **not** the WizFlow/WizCRM VPS (`161.97.141.220`).

Only **WizAccountant.Api** runs in the cloud (Docker). The **WizConnector** stays on the client PC with Sage.

## One-time setup

1. Copy credentials (never commit):

   ```text
   config/secrets/ascendbooks-server.credentials.example.txt
     → config/secrets/ascendbooks-server.credentials.txt
   ```

2. Add `HOST`, `USER`, and either `PASSWORD` or `SSH_PRIVATE_KEY`.

3. Ensure DNS **app.ascendbooks.biz** → `167.86.125.230`.

4. On the server, allow GitHub deploy (SSH key in `/root/.ssh` or use HTTPS clone in bootstrap).

## Publish (GitHub only — no SCP / local upload)

Code on the server always comes from **GitHub** (`git fetch` + `git reset --hard origin/main`).  
Do not copy project folders from your PC.

```powershell
cd C:\Users\pj\WizAccountant
git add -A && git commit -m "your message"   # if needed
git push origin main
.\scripts\deploy-ascendbooks.ps1
```

SSH uses `~/.ssh/contabo_wizerp` (see `config/secrets/website-hosting-notes.md`).

Server one-liner (after push):

```bash
ssh -i ~/.ssh/contabo_wizerp root@167.86.125.230 \
  "cd /opt/wizaccountant && git fetch origin && git reset --hard origin/main && bash scripts/deploy-vps-wizaccountant.sh"
```

## Verify

- https://app.ascendbooks.biz/health → `{"ok":true,...}`
- Admin: https://app.ascendbooks.biz/admin/
- Insight: https://app.ascendbooks.biz/insight/
- Act: https://app.ascendbooks.biz/act/

Pair the on-prem connector to this cloud URL (not localhost).

## Notes

- SQLite data persists in Docker volume `wizaccountant-data`.
- Caddy must be installed on the VPS; deploy appends `caddy-wizaccountant.snippet` if missing.
- Pilot login: `admin@pilot.local` / `pilot` (change before real go-live).
