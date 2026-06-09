# Website hosting notes — Contabo VPS (WizCRM)

Playbook from publishing **WizCRM** on a Contabo Ubuntu VPS. Another agent can reuse this pattern for a **different app** on a **different VPS** by swapping names, domains, and repo paths.

**WizCRM production (reference):**

| Item | Value |
|------|--------|
| Provider | Contabo, Ubuntu 24.04 |
| Server IP | `161.97.141.220` |
| API URL | `https://api.wizcrm.app` |
| Web URL | `https://app.wizcrm.app` |
| App root on server | `/opt/wizcrm` |
| Git branch | `development` |
| Repo | `git@github.com-pj-nrb-ke:pj-nrb-ke/WizCRM.git` |

---

## Architecture (what we run)

```text
Internet
   │
   ▼
Caddy (:443 TLS, auto Let's Encrypt)
   ├── api.wizcrm.app  → reverse_proxy 127.0.0.1:3000  (Node Fastify API)
   └── app.wizcrm.app  → file_server /var/www/wizcrm-web  (Vite static SPA)

127.0.0.1:3000  →  wizcrm-api (systemd, node dist/index.js)
127.0.0.1:5432  →  PostgreSQL (Docker, localhost only)
```

**Why this split**

- **Caddy** — HTTPS termination, no manual cert management.
- **API on systemd** — simple restarts after `git pull` + build; logs via `journalctl`.
- **Postgres in Docker** — isolated DB, data volume, not exposed publicly (`127.0.0.1:5432` only).
- **Web as static files** — `npm run web:build` → copy `dist/`; no Node process for the React app.

---

## Phase 0 — Before touching the server

### 0.1 DNS

At your domain registrar, add **A records** pointing to the VPS IP:

| Host | Points to |
|------|-----------|
| `api` | `161.97.141.220` |
| `app` | `161.97.141.220` |

Wait until `nslookup api.yourdomain.com` returns the VPS IP.

### 0.2 GitHub repo reachable from the server

- Repo must be **cloneable** from the VPS (`git clone` over HTTPS or SSH deploy key).
- WizCRM uses public repo + `development` branch (empty `main` was a pitfall early on).

### 0.3 SSH from your PC to the VPS (no password in chat)

On **Windows** (developer machine):

```powershell
ssh-keygen -t ed25519 -f $env:USERPROFILE\.ssh\contabo_wizcrm -N '""'
```

Add `contabo_wizcrm.pub` to the VPS: **root** `~/.ssh/authorized_keys` (or a deploy user with sudo).

Optional `~/.ssh/config`:

```text
Host contabo-wizcrm
  HostName 161.97.141.220
  User root
  IdentityFile ~/.ssh/contabo_wizcrm
  IdentitiesOnly yes
```

Test: `ssh contabo-wizcrm` or `ssh -i ~/.ssh/contabo_wizcrm root@161.97.141.220`.

### 0.4 Git push from PC without GitHub account popups

Use **SSH** for GitHub with a dedicated host alias (see `scripts/setup-github-ssh.ps1`). Remote example:

`git@github.com-pj-nrb-ke:pj-nrb-ke/WizCRM.git`

---

## Phase 1 — First-time server bootstrap

Run **once** on a fresh VPS as **root**.

### 1.1 Packages

Installed via `scripts/server-setup-contabo.sh`:

- `git`, `curl`, `ufw`, `docker.io`, `caddy`
- **Node.js 22** (NodeSource)
- Docker Compose v2 (or `docker-compose` fallback)

### 1.2 Firewall (UFW)

```text
allow OpenSSH, 80, 443
deny everything else incoming
```

Postgres is **not** opened on 5432 publicly.

### 1.3 Clone application

```bash
mkdir -p /opt/wizcrm
git clone -b development https://github.com/pj-nrb-ke/WizCRM.git /opt/wizcrm
```

For another project: use `/opt/<appname>` and the correct branch.

### 1.4 PostgreSQL (Docker)

File: `docker/docker-compose.prod.yml`

- Container `wizcrm-postgres`, Postgres 16
- Binds `127.0.0.1:5432:5432` only
- Password in `docker/.env.db` (generated once, `chmod 600`)

```bash
cd /opt/wizcrm
# creates docker/.env.db if missing
docker compose -f docker/docker-compose.prod.yml --env-file docker/.env.db up -d
```

### 1.5 API environment

File: `/opt/wizcrm/api/.env` (never commit; `chmod 600`)

Typical keys:

```env
DATABASE_URL=postgresql://wizcrm:<POSTGRES_PASSWORD>@127.0.0.1:5432/wizcrm
PORT=3000
HOST=127.0.0.1
JWT_SECRET=<random hex>
OPENAI_API_KEY=<set on server for AI>
OPENAI_MODEL=gpt-4o-mini
NODE_ENV=production
```

`OPENAI_API_KEY` was added later via editor/SCP on the server, then `systemctl restart wizcrm-api`.

### 1.6 Build API on server

```bash
cd /opt/wizcrm
npm install
npm run build -w shared    # required before api tsc
cd api
npx prisma db push
npm run build
npm run db:seed
```

**Lesson:** monorepo must build **`shared`** before **`api`**, or TypeScript imports from `@wizcrm/shared` fail.

### 1.7 systemd service for API

Unit: `/etc/systemd/system/wizcrm-api.service`

- `WorkingDirectory=/opt/wizcrm/api`
- `EnvironmentFile=/opt/wizcrm/api/.env`
- `ExecStart=/usr/bin/node dist/index.js`
- `After=docker.service` (Postgres up first)

```bash
systemctl daemon-reload
systemctl enable wizcrm-api
systemctl restart wizcrm-api
```

Local health: `curl http://127.0.0.1:3000/health`

### 1.8 Caddy — API only (first cut)

`/etc/caddy/Caddyfile`:

```text
api.wizcrm.app {
    reverse_proxy 127.0.0.1:3000
}
```

```bash
systemctl enable caddy
systemctl reload caddy
```

Public check: `https://api.wizcrm.app/health` → `{"status":"ok",...}`

---

## Phase 2 — Static web app (admin / manager UI)

### 2.1 Build on server (or CI)

API URL is baked at build time for Vite:

```bash
cd /opt/wizcrm
VITE_API_URL=https://api.wizcrm.app npm run web:build
```

Output: `web/dist/`

### 2.2 Publish files

```bash
mkdir -p /var/www/wizcrm-web
rm -rf /var/www/wizcrm-web/*
cp -r /opt/wizcrm/web/dist/* /var/www/wizcrm-web/
```

For another app use `/var/www/<app>-web`.

### 2.3 Caddy — add web site

Append to `/etc/caddy/Caddyfile`:

```text
app.wizcrm.app {
    root * /var/www/wizcrm-web
    encode gzip
    file_server
    try_files {path} /index.html
}
```

`try_files` is required for React Router (SPA deep links).

```bash
caddy validate --config /etc/caddy/Caddyfile
systemctl reload caddy
```

Check: `https://app.wizcrm.app` returns the login page (HTTP 200).

---

## Phase 3 — Ongoing deploys (every code change)

**Do not SCP the whole project** — use **git pull** on the server (large uploads were interrupted in practice).

### 3.1 Automated script (repo)

- On server logic: `scripts/deploy-vps.sh`
- From Windows: pipe script over SSH (LF line endings; stash server dirt before pull):

```powershell
Get-Content scripts/deploy-vps.sh -Raw | ForEach-Object { $_ -replace "`r`n","`n" } |
  ssh -i $env:USERPROFILE\.ssh\contabo_wizcrm root@161.97.141.220 "bash -s"
```

Or: `.\scripts\deploy-vps.ps1` after `git push`.

### 3.2 Deploy steps (in order)

1. `git pull origin development` (stash if server has stray edits e.g. `tsconfig.tsbuildinfo`)
2. `npm install`
3. `npm run build -w shared`
4. `cd api && npx prisma db push && npm run build`
5. `VITE_API_URL=https://api.wizcrm.app npm run web:build`
6. Copy `web/dist/*` → `/var/www/wizcrm-web/`
7. `systemctl restart wizcrm-api`
8. `systemctl reload caddy`
9. Smoke: `curl https://api.wizcrm.app/health`

### 3.3 Local refresh (developer PC)

`scripts/refresh-local.ps1` — rebuild shared, api, web; user only refreshes browser / tests app.

---

## Phase 4 — Mobile app pointing at production

- Production API: `https://api.wizcrm.app` (**no** `:3000` on HTTPS).
- APK build: `.\scripts\build-apk.ps1 -ApiUrl "https://api.wizcrm.app"`
- Runtime override on device: `api-url.txt` or in-app Save API URL (`scripts/push-api-url.ps1`).

---

## Pitfalls we hit (save time on the next VPS)

| Issue | Cause | Fix |
|--------|--------|-----|
| `git clone` failed | Private repo or empty `main` | Public repo or deploy key; clone `-b development` |
| API build fails on server | `@wizcrm/shared` not built | Always `npm run build -w shared` before `api` |
| `git pull` fails on server | Local changes on VPS | `git stash` before pull (in deploy script) |
| Deploy script `pipefail` error | CRLF in bash from Windows | Strip `\r` when piping script, or `set -eu` only |
| HTTPS timeout to `:3000` | Port 3000 not public | Use `https://api.domain` without port |
| Slow desk tab | LLM on every load | Rules-first desk; optional AI via env/DB setting |
| Web account popup on push | HTTPS + multiple GitHub accounts | SSH remote + key on correct account |
| Android Documents path | Scoped storage | Push `api-url.txt` to app-specific path |
| `expo-file-system` import | New API throws | Use `expo-file-system/legacy` in mobile |
| OpenAI errors | Empty `OPENAI_API_KEY` on server | Set in `/opt/wizcrm/api/.env`, restart API |
| Prisma schema drift | New columns e.g. `Organization.settings` | `npx prisma db push` on server after pull |

---

## Checklist for a **new** app on a **new** VPS

Copy and replace placeholders:

1. [ ] VPS provisioned (Ubuntu LTS), root SSH key installed  
2. [ ] DNS A records → new IP (`api.<domain>`, `app.<domain>`)  
3. [ ] `apt`: docker, caddy, git, node 22, ufw (22/80/443)  
4. [ ] `git clone` to `/opt/<app>`  
5. [ ] Docker Postgres (or managed DB) — **localhost only**  
6. [ ] `api/.env` with secrets on server only (`chmod 600`)  
7. [ ] `npm install` + build order (`shared` → `api`)  
8. [ ] `prisma db push` / migrations + seed  
9. [ ] systemd unit for Node API on `127.0.0.1:3000`  
10. [ ] Caddy `reverse_proxy` for API hostname  
11. [ ] `VITE_API_URL=... npm run web:build` → `/var/www/<app>-web`  
12. [ ] Caddy `file_server` + `try_files` for SPA  
13. [ ] Health URL over HTTPS  
14. [ ] Deploy script: pull → build → copy static → restart services  
15. [ ] Document test logins / smoke URLs for the product owner (no secrets in git)

---

## Useful commands (WizCRM)

```bash
# Logs
journalctl -u wizcrm-api -f

# API restart after .env change
systemctl restart wizcrm-api

# Postgres
docker ps
docker logs wizcrm-postgres

# Caddy
caddy validate --config /etc/caddy/Caddyfile
systemctl reload caddy
```

```powershell
# SSH from Windows
ssh -i $env:USERPROFILE\.ssh\contabo_wizcrm root@161.97.141.220

# Deploy from repo root (after git push)
.\scripts\deploy-vps.ps1
```

---

## Related repo files

| File | Purpose |
|------|---------|
| `scripts/server-setup-contabo.sh` | One-time VPS bootstrap |
| `scripts/deploy-vps.sh` | Repeat deploy on server |
| `scripts/deploy-vps.ps1` | Run deploy via SSH from Windows |
| `scripts/refresh-local.ps1` | Local builds |
| `docker/docker-compose.prod.yml` | Production Postgres |
| `web/README.md` | Web build + Caddy snippet |
| `docs/PARALLEL-AGENT-NOTE.md` | Agent ops (user does not run scripts) |

---

---

## WizERP production (this repo — PHP / MariaDB)

| Item | Value |
|------|--------|
| Provider | Contabo VPS |
| Server IP | `167.86.125.230` |
| Web URL | `https://wizerp.app` (recommended) |
| DNS | A record `@` (root) → `167.86.125.230`; optional `www` → same IP |
| Legacy URL | `https://app.wizerp.app` redirects to `https://wizerp.app` |
| App root on server | `/opt/wizerp` |
| Git branch | `WizERP-Workflow` |
| Repo | `git@github.com-pj-nrb-ke:pj-nrb-ke/WizERP.git` |

### Architecture

```text
Internet → Caddy (:443, Let's Encrypt)
              ├── wizerp.app (+ www) → reverse_proxy 127.0.0.1:8080
              └── app.wizerp.app → redirect to https://wizerp.app
                        └── Docker: wizerp-web (PHP 8.1 + Apache)
                        └── Docker: wizerp-db (MariaDB 10.11, 127.0.0.1:3306 only)
```

Unlike WizCRM, there is **no Node API** — the ERP is PHP with MariaDB (same stack as local `docker compose`).

### SSH from Windows

```powershell
ssh-keygen -t ed25519 -f $env:USERPROFILE\.ssh\contabo_wizerp -N '""'
```

Add `contabo_wizerp.pub` to VPS **root** `~/.ssh/authorized_keys` (Contabo panel → SSH keys, or paste in console).

`~/.ssh/config`:

```text
Host contabo-wizerp
  HostName 167.86.125.230
  User root
  IdentityFile ~/.ssh/contabo_wizerp
  IdentitiesOnly yes
```

The VPS also needs the **same GitHub deploy key** (`github_pj_nrb_ke`) in `root@server:~/.ssh/` to `git clone` (or use a read-only deploy key on repo).

### One-time bootstrap (on server)

```powershell
Get-Content scripts/server-setup-wizerp.sh -Raw | ForEach-Object { $_ -replace "`r`n","`n" } |
  ssh -i $env:USERPROFILE\.ssh\contabo_wizerp root@167.86.125.230 "bash -s"
```

Or after `git push`: clone on server and `bash /opt/wizerp/scripts/server-setup-wizerp.sh`.

### Ongoing deploy

```powershell
.\scripts\deploy-vps-wizerp.ps1
```

### DNS at your registrar (use `wizerp.app` as the main address)

Add these **A records** (all point to **`167.86.125.230`**):

| Host / name | Type | Value | Purpose |
|-------------|------|--------|---------|
| `@` (or leave blank for “root”) | A | `167.86.125.230` | **https://wizerp.app** — main ERP URL |
| `www` (optional) | A | `167.86.125.230` | **https://www.wizerp.app** |
| `app` (optional, already set) | A | `167.86.125.230` | Old URL; server redirects to `wizerp.app` |

Remove or ignore **CNAME** conflicts on `@` if the registrar warns you. Wait 15 minutes–48 hours for DNS to propagate worldwide.

Until `@` is set, the app still works at **https://app.wizerp.app**.

### Demo login (after import)

- **URL:** https://wizerp.app/ (or https://app.wizerp.app/ until DNS is ready)
- **User:** `admin` / **Password:** `password` (change immediately)
- Remove `install/` directory after confirming access.

### WizERP scripts

| File | Purpose |
|------|---------|
| `scripts/server-setup-wizerp.sh` | One-time VPS bootstrap |
| `scripts/deploy-vps-wizerp.sh` | Pull + rebuild containers |
| `scripts/deploy-vps-wizerp.ps1` | Run deploy from Windows |
| `docker/docker-compose.prod.yml` | Production MariaDB + web |

---

*Last aligned with WizCRM `development` branch after Cluster A/B web deploy and SSH GitHub setup.*
