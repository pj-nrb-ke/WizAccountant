# WizAccountant Plan 1 — Phase-wise feature split

**Architecture:** On-prem **WizConnector** (.NET + Sage Evolution SDK) ↔ cloud **WizAccountant** (web + API + **mobile apps**) over **outbound HTTPS / WebSocket**. No WizVPN. Client keeps Sage 200 Evolution as system of record.

**Mobile:** iOS/Android apps use the **same cloud API** as the web app (no Sage SDK on device). See phase sections below.

**Principles (all phases):**
- Allowlisted operations only (no raw SDK / SQL from UI or AI)
- Reads before writes; writes require audit + (from Phase 3) explicit approval
- Every job: `jobId`, `tenantId`, `siteId`, `idempotencyKey` for posts
- UI shows **site status** (online/offline) and Sage company name

---

## Phase 1 — Connect & read (foundation)

**Goal:** Prove secure pairing, live data from customer Sage, and first useful screens. **No AI posting. No writes to ledger.**

**Duration guide:** 8–12 weeks (team-dependent)

### WizConnector (on-prem)

| Feature | Details |
|---------|---------|
| Windows Service | Auto-start, recovery, structured logging |
| System tray app | Pairing wizard, connection status, device/site ID, open logs |
| Pairing | One-time code from cloud → store `siteId` + credentials (DPAPI) |
| Cloud link | Outbound TLS; WebSocket (or SignalR) + REST fallback long-poll |
| Heartbeat | Every 30–60s; version, SDK/Evolution version, company DB name |
| Local config | Evolution connection profile, SDK agent user (encrypted) |
| Health handler | `Site.Health` — DB reachable, SDK auth OK |

### Sage handlers (read-only)

| Domain | Operations |
|--------|------------|
| GL | `GLAccount.List`, `GLAccount.Get` |
| AR | `Customer.List`, `Customer.Get`, `CustomerTransaction.List` (criteria) |
| AP | `Supplier.List`, `Supplier.Get`, `SupplierTransaction.List` (criteria) |
| Reports / extracts | Trial balance–style list via transactions/accounts (scoped queries) |
| Inventory (optional) | `InventoryItem.List`, `InventoryItem.Get` |

### WizAccountant (cloud)

| Feature | Details |
|---------|---------|
| Auth | Email/password or SSO stub; tenants + users |
| Sites | Register tenant → generate pairing code → list sites, last seen |
| Connector Hub | Route jobs to connected `siteId`; job queue + timeout |
| Admin UI | Sites list, connection status, “Test connection” button |
| Job API | Submit job → wait/poll result (sync read with 30–60s cap) |
| Audit (basic) | Log job type, user, site, timestamp, success/fail |

### WizAccountant Mobile (iOS / Android)

| Feature | Details |
|---------|---------|
| — | **Not in Phase 1** — API designed mobile-ready (JWT, same job endpoints) |

### Out of scope Phase 1

- AI chat, workflows, writes, multi-site analytics, **mobile app (ships Phase 2)**
- Inventory documents, orders `Process()`, allocations

### Phase 1 success criteria

- [ ] Pair and run reads on 2 pilot sites (different Evolution versions documented)
- [ ] Site shows **Online** within 60s of service start
- [ ] Read customer list + AR open items without desktop Sage open
- [ ] No inbound ports on customer firewall

---

## Phase 2 — Read product + AI assistant (read-only)

**Goal:** Daily-use web experience and AI on **live** data. Still **no ledger writes** (or internal-only write tests).

**Duration guide:** 8–12 weeks after Phase 1

### WizConnector

| Feature | Details |
|---------|---------|
| Expanded reads | Outstanding balances, aged analysis inputs, transaction detail by `Autoidx` |
| Search / filter | Standardized criteria builders (account, date range, reference) |
| Performance | Connection pooling per company DB; query timeouts; pagination |
| Diagnostics | Export support bundle (redacted logs, versions, last 50 jobs) |
| Auto-update (optional) | Signed installer check channel |

### Sage handlers (read-only)

| Domain | Operations |
|--------|------------|
| AR | Open items / outstanding; allocations **read** (PostAR view via list) |
| AP | Same for suppliers |
| GL | Transaction list by account/period |
| Inventory | Stock levels, item search (if in scope for pilots) |
| Orders (read) | `SalesOrder.List`, `PurchaseOrder.List` unprocessed/archived flags |
| Master data | Projects, warehouses, tax rates, transaction codes **list** (for AI context) |

### WizAccountant (cloud)

| Feature | Details |
|---------|---------|
| Dashboard | TB summary, bank of KPIs (debtors/creditors total — from reads) |
| AR/AP workspaces | Customer/supplier browse, drill to transactions |
| Global search | Cross-entity search (customers, suppliers, accounts, refs) |
| **AI chat (read-only)** | Tools mapped 1:1 to read handlers; citations (account, ref, date) |
| Conversation history | Per tenant/user; no PII in model logs policy documented |
| Export | CSV/PDF for lists user already can see |
| Notifications | Email (e.g. Brevo) for invites, site offline alerts |

### WizAccountant Mobile (iOS / Android)

| Feature | Details |
|---------|---------|
| Auth | Login, biometric unlock (Face ID / fingerprint), secure token storage |
| Sites | List sites, online/offline status, switch active site/tenant |
| Dashboard | Debtors/creditors totals, TB snapshot (read-only) |
| AR/AP quick view | Customer/supplier search, open items list |
| Push notifications | Site offline alert; optional daily summary |
| AI chat (read-only) | Same tools as web Phase 2; mobile-optimised UI |
| Deep links | Open entity from notification (customer, approval preview in P3) |

**Tech note:** React Native or Flutter recommended; shares REST + WebSocket with web.

### AI guardrails (Phase 2)

- Tool allowlist = read handlers only
- System prompt: never claim to have posted; suggest “request approval” for actions
- Show **data as of** job completion time

### Phase 2 success criteria

- [ ] User asks natural language question → correct AR/AP answer on pilot data
- [ ] 5 read tools used in production chat without SDK errors >99% success
- [ ] Accountant prefers browser for enquiry vs opening Evolution for those tasks
- [ ] Mobile: login, dashboard, and one AR enquiry flow on iOS or Android

---

## Phase 3 — Controlled writes + approvals

**Goal:** High-value **posting** and allocations with human-in-the-loop. Production-grade audit.

**Duration guide:** 12–16 weeks after Phase 2

### WizConnector

| Feature | Details |
|---------|---------|
| Idempotency store | Local SQLite: `idempotencyKey` → result hash (prevent duplicate posts) |
| Write handlers | See table below |
| Transactions | `DatabaseContext.BeginTran` / `Commit` / `Rollback` per business action |
| `StartNewBatch` | Separate audit batches per post where appropriate |
| Error mapping | Evolution exceptions → stable error codes for UI |
| Consent (optional) | Tray prompt: “Allow cloud to post for 1 hour” per session |

### Sage handlers (writes — allowlisted)

| Domain | Operations | Notes |
|--------|------------|-------|
| GL | `GLTransaction.Post` (balanced journal); journal batch **create only** if needed | VAT legs per SDK docs |
| AR | `CustomerTransaction.Post`; `Allocations.Save` | Codes configurable per site |
| AP | `SupplierTransaction.Post`; `Allocations.Save` | Same |
| Master (limited) | `Customer.Save`, `Supplier.Save` | Create/edit — role-gated |
| Orders (optional v3b) | `SalesOrder` / `PurchaseOrder` `Save` + `Process` partial | High complexity — sub-phase |

**Explicitly deferred or create-only (SDK limits):**
- Cashbook / journal batch **process** (not supported by SDK)
- Job card **process/complete**
- Evolution **printing**

### WizAccountant (cloud)

| Feature | Details |
|---------|---------|
| Approval queue | Proposed journal/payment → approver → execute job |
| Maker-checker | Configurable per tenant (role: prepare vs approve) |
| AI propose, human approve | AI returns structured **draft**; post only after approval |
| Write audit | Before/after payload, user, approver, Evolution audit ref if returned |
| Workflows (starter) | Templates: month-end checklist, payment run proposal |
| Site config sync | Transaction codes, default tax, branch per site |
| Rollback messaging | Clear: SDK posts are not auto-reversed in product |

### WizAccountant Mobile (iOS / Android)

| Feature | Details |
|---------|---------|
| Approval inbox | List pending journals/payments/allocations |
| Approve / reject | Maker-checker actions with comment |
| Push notifications | “Approval required” with deep link to item |
| AI drafts | View AI-proposed entry; approve redirects to approval flow |
| Audit | View approval history for item (read-only) |

### Phase 3 success criteria

- [ ] End-to-end: propose GL journal → approve → single balanced post in live Sage
- [ ] Duplicate submit with same idempotency key does not double-post
- [ ] Allocation payment to invoice on pilot AR
- [ ] Full audit trail export for one transaction
- [ ] Mobile: approver completes one pending payment from push notification

---

## Phase 4 — Scale, inventory & enterprise

**Goal:** Broader module coverage, multi-site practices, ops maturity.

**Duration guide:** ongoing after Phase 3

### WizConnector

| Feature | Details |
|---------|---------|
| Multi-company | One agent → multiple company DBs (if Evolution licensed) |
| Branch context | `DatabaseContext.SetBranchContext` per site config |
| Heavy modules | Credit note, RTS, inventory adj, warehouse transfer (SDK) |
| Order processing | Sales/purchase `Process` / partial / `Complete` with strict tests |
| Offline queue | Buffer approved jobs when cloud reachable but Sage busy (optional) |

### WizAccountant (cloud)

| Feature | Details |
|---------|---------|
| Practice mode | One firm, many client sites; switch context |
| RBAC | Fine-grained: read vs propose vs approve vs admin |
| SSO | Azure AD / Google (enterprise) |
| Monitoring | Site SLA dashboard, failed job alerts |
| Billing | Per site / per user subscription hooks |
| Compliance | Data processing agreement templates, retention policy |
| Advanced AI | Multi-step workflows, document upload → draft (future) |

### Sage handlers (Phase 4 examples)

| Domain | Operations |
|--------|------------|
| Inventory | `InventoryTransaction.Post`, CN/RTS `Process`, warehouse transfer |
| Orders | Full SO/PO lifecycle per SDK portal patterns |
| CRM | Incidents (if product needs) |
| Job costing | Job card `Save` only (no process) — UI sets expectations |

### WizAccountant Mobile (iOS / Android)

| Feature | Details |
|---------|---------|
| Practice mode | Switch client site (accounting firms) |
| Inventory (read) | Stock lookup, low-stock alert push |
| Workflows | Run starter checklist steps from phone |
| Tablet layout | Optimised for approvers on iPad/Android tablet |
| App store release | Production signing, crash analytics, staged rollout |

### Phase 4 success criteria

- [ ] 20+ sites under management with monitoring
- [ ] At least one inventory workflow in production
- [ ] Enterprise pilot with SSO + practice multi-site

---

## Cross-phase platform features

Build incrementally; not all required in Phase 1.

| Capability | Phase introduced |
|------------|------------------|
| Shared contracts NuGet (`JobRequest` / `JobResult`) | 1 |
| PostgreSQL (tenants, sites, jobs, audit) | 1 |
| SignalR / WebSocket hub | 1 |
| Serilog + correlation id | 1 |
| DPAPI secrets on connector | 1 |
| Rate limiting per site | 2 |
| API versioning | 2 |
| CI: connector MSI + API docker | 1 |
| Evolution version matrix doc | 1 |
| Push notifications (FCM / APNs) | 2 |
| Mobile apps (iOS + Android) | 2 |
| Mobile approval UX | 3 |

---

## Suggested module priority (Sage SDK alignment)

Aligned with your `DOCS` SDK guides:

1. **GL** — accounts, journals (Phase 1–3)
2. **AR / AP** — masters, transactions, allocations (Phase 1–3)
3. **Inventory** — items, adjustments (Phase 2 read, Phase 4 write)
4. **Sales / Purchase orders** — Phase 3b / 4 (complexity high)
5. **CRM / Job costing** — Phase 4 or later (niche)

---

## What we are not building in Plan 1 roadmap

- WizVPN / network tunnel to LAN
- Hosted customer DB restore as primary product
- Full cloud replacement of Evolution UI (Plan 3)
- Raw SQL access from web or AI
- Evolution report printing via SDK
- Unrestricted SDK passthrough

---

## Release naming (optional)

| Release | Phases |
|---------|--------|
| **v0.1 Connect** | Phase 1 (web only) |
| **v0.5 Insight** | Phase 2 (web + mobile read) |
| **v1.0 Act** | Phase 3 (web + mobile approvals) |
| **v1.x Scale** | Phase 4 (web + mobile enterprise) |

---

*Last updated: 2026-05-26 — Plan 1 (WizConnector + WizAccountant cloud)*
