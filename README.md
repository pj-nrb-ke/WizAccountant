# WizAccountant

Accounting application repository.

## Git (no account pop-up)

This repo uses **SSH** for `pj-nrb-ke` (not HTTPS), so Git Credential Manager will not ask you to pick a GitHub account.

Remote: `git@github.com-pj-nrb-ke:pj-nrb-ke/WizAccountant.git`

After clone, or if you see the GitHub account picker again:

```powershell
powershell -File scripts/setup-github-ssh.ps1
```

## Phase 1 scaffold (started)

Current solution/projects:

- `src/WizAccountant.Contracts` — shared DTOs (`Pairing`, `Site`, `Job`, `Heartbeat`)
- `src/WizAccountant.Api` — pairing code API, site pairing, jobs API, SignalR connector hub, SQLite persistence
- `src/WizConnector.Service` — on-prem worker with pairing flow, hub connection, heartbeat, job execution shell
- `src/WizConnector.Setup` — Sage connection wizard (DPAPI config)
- `src/WizConnector.Tray` — system tray: pairing, status, open Setup

Build:

```powershell
dotnet build WizAccountant.slnx
```

### Sage SDK

Installed locally at `C:\Program Files (x86)\Sage Evolution` (v11). See [lib/sage-sdk/README.md](lib/sage-sdk/README.md) and [official SDK downloads](https://developerzone.pastel.co.za/index.php?title=Downloads).

Configure Sage via **`WizConnector.Setup.exe`** (saves encrypted config). See [DOCS/SAGE-Connection-Process.md](DOCS/SAGE-Connection-Process.md).

**Pilot (API + connector):** see [scripts/run-pilot-e2e.ps1](scripts/run-pilot-e2e.ps1) — API on `http://localhost:5278`.

## Phase 3 Act (approvals + writes)

- **Act UI:** [http://localhost:5278/act/](http://localhost:5278/act/) — approval inbox, propose postings, write audit
- **Roles:** preparer@pilot.local / approver@pilot.local / admin@pilot.local (password `pilot`)
- **Docs:** [DOCS/PHASE3-APPROVALS.md](DOCS/PHASE3-APPROVALS.md)
- Enable live posts: `Connector:WritesEnabled=true` + Tray → **Allow cloud posts (1 hour)**

## Phase 2 Insight (web)

- **Insight UI:** [http://localhost:5278/insight/](http://localhost:5278/insight/) — dashboard, AR/AP workspaces, search, read-only AI chat
- **Admin:** [http://localhost:5278/admin/](http://localhost:5278/admin/)
- **AI guardrails:** [DOCS/PHASE2-AI-GUARDRAILS.md](DOCS/PHASE2-AI-GUARDRAILS.md)
- Dev login: `admin@pilot.local` / `pilot` → `POST /api/auth/login`

**Admin UI:** run the API, then open [http://localhost:5278/admin/](http://localhost:5278/admin/) — pairing codes, sites, test connection.

**Tray:** `src\WizConnector.Tray\bin\Release\net8.0-windows\WizConnector.Tray.exe` (pair site, view online status).

## Pilot launcher (buttons — no command line)

Double-click **WizPilot.exe** to start the connector, tray, open Admin/Insight, and run scripts:

`src\WizAccountant.Manager\bin\Release\net8.0-windows\WizPilot.exe`

Rebuild after code changes: `.\scripts\build-wiz-pilot.ps1`
