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

## Phase 2 Insight (web) — Phase 4 ready ✅

- **Insight UI:** [http://localhost:5278/insight/](http://localhost:5278/insight/) — dashboard, AR/AP workspaces, search, read-only AI chat
- **Admin:** [http://localhost:5278/admin/](http://localhost:5278/admin/)
- **AI guardrails:** [DOCS/PHASE2-AI-GUARDRAILS.md](DOCS/PHASE2-AI-GUARDRAILS.md)
- Dev login: `admin@pilot.local` / `pilot` → `POST /api/auth/login`

**Capability gaps closed (June 2026, pre-Phase 4):**
- GAP-010: Full `HandlerCapabilityRegistry` metadata (96+ operations)
- GAP-011: `gl.period.close.readiness` — 5-check period-close checklist handler
- GAP-012: `OutputContractValidator` strict shapes for all major handler domains
- GAP-013: AP supplier payment discipline — 4 handlers (`supplier.payment.*`)
- GAP-014: Multi-turn entity code persistence via `entity:key:value` tags in ToolsUsedJson
- GAP-030: Treasury explainability — `topContributors`, `likelyCause`, `cashDrivers`
- GAP-031: VAT variance contributors split by DocType (output vs input VAT)
- GAP-020: `site.schema.probe` — INFORMATION_SCHEMA column probe for 13 core tables
- GAP-021: `site.metadata` — connector version + key-table presence + capability flags
- RBAC v2: `WizRoles` (Reader/Preparer/Approver/Admin/FirmAdmin) + `RbacMiddleware` path enforcement
- FirmRecord + practice mode: firm-level write-block for training/demo environments
- SiteMonitorService: site SLA dashboard, 24h failed-job alerts
- Mobile Phase 4 API: `/api/mobile/app-config` with practice mode + inventory feature flags
- Multi-company `MultiSiteQueryService`: fan-out one operation to all online sites in a firm
- ConnectorWriteAllowlist expanded to 15 operations (Phase 4 Block 3)
- Inventory writes: `inventory.adjustment.post`, `warehouse.transfer.post`
- Credit notes: `salescreditnote.post` (DocType=1), `suppliercreditnote.post` (DocType=3/RTS)
- Order lifecycle: `salesorder.confirm`, `salesorder.ship`, `purchaseorder.approve`, `purchaseorder.receive`
- `ProposalTypeMap` covers all 15 write operations
- SSO: OIDC token validation (Azure AD + Google) via `OidcTokenValidator`; `OidcAuthService` auto-provisions users
- `ExternalIdentityRecord` links (Provider, Subject) → WizAccountant user; survives re-logins
- Billing webhooks: `BillingService` handles Stripe/Paddle events; `SubscriptionRecord` per tenant
- Plan gating: free/pro/enterprise feature flags via `BillingService.IsFeatureEnabledAsync`
- POPIA/GDPR compliance: `ComplianceService` — data export + right-to-erasure redaction
- New routes: `/api/auth/oidc/login`, `/api/billing/webhook`, `/api/billing/subscription/*`, `/api/compliance/*`
- `version.json` app manifest for MSIX / direct download store pipeline

Test suite: **1578 tests, 0 failures** — line coverage 49.9%, branch 52.8%

**Admin UI:** run the API, then open [http://localhost:5278/admin/](http://localhost:5278/admin/) — pairing codes, sites, test connection.

**Tray:** `src\WizConnector.Tray\bin\Release\net8.0-windows\WizConnector.Tray.exe` (pair site, view online status).

## Pilot launcher (buttons — no command line)

Double-click **WizPilot.exe** to start the connector, tray, open Admin/Insight, and run scripts:

`src\WizAccountant.Manager\bin\Release\net8.0-windows\WizPilot.exe`

Rebuild after code changes: `.\scripts\build-wiz-pilot.ps1`
