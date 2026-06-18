# Agent Guide: Accessing Local Sage Evolution from the Cloud via WizConnector

> **Audience:** Claude AI agents (and developers) building features that need to read or write data in a user's locally-installed Sage Evolution (Pastel) database through the WizAccountant / AscendBooks platform.

---

## 1. Big Picture

The user's Sage Evolution database lives on their Windows PC behind a firewall. The cloud API (`WizAccountant.Api`, running in Docker) **cannot connect directly** to it. The bridge is a trio of local Windows components collectively called **WizPilot**:

```
┌────────────────────────────────────────────────────┐
│  USER'S WINDOWS PC                                 │
│                                                    │
│  ┌──────────────┐   launch   ┌──────────────────┐  │
│  │  WizPilot    ├───────────►│WizConnector.Tray │  │
│  │  (Manager)   │            │  (System Tray)   │  │
│  └──────┬───────┘            └────────┬─────────┘  │
│         │ launch                      │ pairing UI  │
│         ▼                             │             │
│  ┌──────────────┐   Sage SDK   ┌──────▼─────────┐  │
│  │WizConnector  │◄────────────►│  Sage Evolution │  │
│  │  .Service    │              │  SQL Server DB  │  │
│  └──────┬───────┘              └─────────────────┘  │
│         │  SignalR / REST poll                       │
└─────────┼──────────────────────────────────────────┘
          │  (outbound HTTPS — no firewall hole needed)
          ▼
┌─────────────────────────────────────────────────────┐
│  CLOUD (Docker on localhost:8088 / prod)            │
│                                                     │
│  WizAccountant.Api                                  │
│  ├── ConnectorHub  (SignalR)                        │
│  ├── JobService    (queue + dispatch)               │
│  ├── AppDbContext  (SQLite — sites / jobs / audit)  │
│  └── REST endpoints (pairing, jobs, insight …)      │
└─────────────────────────────────────────────────────┘
```

**Key point:** all traffic is **outbound from the PC** over a persistent SignalR WebSocket (or a long-poll HTTP fallback). The cloud never opens a socket *to* the PC.

---

## 2. The Three Local Components

### 2.1 WizPilot (WizAccountant.Manager) — `WizPilot.exe`

The master launcher. When the user runs `WizPilot.exe` it:

1. Reads `pilot.config.json` (AI-managed config file in the repo root) for `ApiBaseUrl`.
2. Starts `WizConnector.Service.exe` as a child process, injecting `Connector__ApiBaseUrl` as an environment variable override.
3. Starts `WizConnector.Tray.exe` so the user has a tray icon.

**Config priority (highest first):**
| Source | Path | Who writes it |
|--------|------|---------------|
| `pilot.config.json` | repo root | **AI / developer** (committed to git) |
| `pilot-launcher.json` | `%APPDATA%\WizAccountant\` | User via UI |

`pilot.config.json` always wins. Changing the API URL means editing this file and rebuilding / restarting WizPilot.

### 2.2 WizConnector.Tray — `WizConnector.Tray.exe`

A Windows Forms system-tray app. Its sole job during normal operation is:

- Show the user which site is paired and whether it is Online.
- Provide a "Pair with code…" dialog so the user can enter a pairing code from the web admin panel.
- Provide a "Status…" window with live Online/Offline state, editable API URL, and a Refresh button.
- Write the paired-site state to `C:\ProgramData\WizConnector\connector-state.json` after a successful pairing.

### 2.3 WizConnector.Service — `WizConnector.Service.exe`

The real workhorse — a .NET Generic Host background service (`Worker.cs`). It:

1. Reads `connector-state.json` to find its `SiteId`.
2. Connects to the cloud API's SignalR hub (`/connectorHub`).
3. Registers itself as online (`RegisterSite(siteId)`).
4. Sends a heartbeat every 30 s.
5. Listens for `RunJob` messages and executes them against the local Sage SDK.
6. Falls back to REST long-polling when SignalR is down.

---

## 3. Pairing — How a Site Gets Registered

Pairing links a specific installation of WizConnector to a cloud `SiteRecord`. It must happen **once** before any Sage data can flow.

### Step-by-step

```
Admin panel (web)          WizAccountant.Api               WizConnector.Tray
──────────────────         ─────────────────────           ─────────────────
POST /api/sites/
  pairing-codes            → creates PairingCodeRecord
  {ExpiresInMinutes:60}    ← returns {Code:"WZ307183", …}

(user copies code, enters in tray)

                           POST /api/sites/pair
                           {PairingCode:"WZ307183",
                            DeviceId:"PJ-PC",
                            ConnectorVersion:"0.1.0-tray"}
                           → validates code not expired/used
                           → creates SiteRecord {SiteId, TenantId, SiteName, DeviceId}
                           → marks PairingCode.Used = true
                           ← {SiteId, TenantId, SiteName}

                                                           saves connector-state.json:
                                                           {
                                                             "siteId": "<guid>",
                                                             "tenantId": "pilot-tenant",
                                                             "siteName": "PJ"
                                                           }
```

**After pairing the service must be restarted** so `Worker.cs` picks up the new `SiteId` from the state file and re-registers with the hub.

### Re-pairing

Pairing codes are one-use. To re-pair:
1. Generate a new code in the admin panel (`POST /api/sites/pairing-codes`, max expiry 7 days).
2. Delete `C:\ProgramData\WizConnector\connector-state.json` if the old site record is stale.
3. Use the new code in the Tray → "Pair with code…".

### File location for state

```csharp
// ConnectorPaths.cs (in WizAccountant.Contracts)
Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "WizConnector",
    "connector-state.json"
);
// → C:\ProgramData\WizConnector\connector-state.json
```

The JSON parser uses `PropertyNameCaseInsensitive = true`, so both `siteId` (camelCase) and `SiteId` (PascalCase) work.

---

## 4. Configuration Files

### 4.1 `pilot.config.json` (repo root — AI-managed)

```json
{
  "ApiBaseUrl": "http://localhost:8088",
  "ProductionUrl": "https://app.ascendbooks.biz"
}
```

**This is the single source of truth for the API URL.** Both WizPilot (Launcher) and WizConnector.Tray read this file via a walk-up search from their exe directory or the hardcoded dev path `C:\Users\pj\WizAccountant\pilot.config.json`.

When you need to change the API URL (port change, new domain, switching between local dev and production), edit this file. No registry, no AppData hunting.

### 4.2 `src/WizConnector.Service/appsettings.json`

```json
{
  "Connector": {
    "ApiBaseUrl": "http://localhost:8088",
    "PairingCode": "",
    "DeviceId": "",
    "ConnectorVersion": "0.1.0",
    "RestJobPollEnabled": true,
    "RestPollWaitSeconds": 30
  },
  "Sage": {
    "Enabled": true,
    "SdkPath": "C:\\Program Files (x86)\\Sage Evolution",
    "CommonConnectionString": "",
    "CompanyConnectionString": "",
    "LicenseSerial": "",
    "LicenseKey": "",
    "AgentUser": "Admin",
    "AgentPassword": "",
    "BranchId": 0,
    "Companies": {}
  }
}
```

Environment variable `Connector__ApiBaseUrl` overrides `ApiBaseUrl` (set by WizPilot at launch). `DeviceId` defaults to `Environment.MachineName` when empty.

### 4.3 Sage config on disk (encrypted)

Sage credentials (SQL Server connection strings, Sage license, agent password) are **not** kept in plain-text `appsettings.json` in production. `SageConfigStorage.LoadEncrypted()` reads `C:\ProgramData\WizConnector\sage.config` (DPAPI-encrypted) and merges it into `builder.Configuration` at startup. The Tray app's "Open Sage Setup" launches `WizConnector.Setup.exe` to write this file.

---

## 5. SignalR Hub — Primary Channel

### Hub endpoint

```
ws://localhost:8088/connectorHub   (or wss:// in prod)
```

### Connector-side (Worker.cs)

```csharp
_hub = new HubConnectionBuilder()
    .WithUrl($"{apiBaseUrl}/connectorHub", opts =>
        opts.Headers["X-Device-Id"] = deviceId)
    .WithAutomaticReconnect()
    .Build();

// On connected:
await _hub.InvokeAsync("RegisterSite", siteId);

// Heartbeat every 30 s:
await _hub.InvokeAsync("Heartbeat", new ConnectorHeartbeat
{
    SiteId = siteId,
    DeviceId = deviceId,
    ConnectorVersion = settings.ConnectorVersion,
    TimestampUtc = DateTimeOffset.UtcNow
});

// Incoming job:
_hub.On<RunJobMessage>("RunJob", async msg =>
{
    var resultJson = await _executor.ExecuteAsync(msg.Operation, msg.Parameters, ct);
    await _hub.InvokeAsync("SubmitResult", new SubmitJobResultRequest
    {
        JobId = msg.JobId,
        ResultJson = resultJson,
        Error = null
    });
});
```

### API-side (ConnectorHub.cs)

```csharp
// Called by connector on connect
public async Task RegisterSite(Guid siteId)
{
    _registry.Register(siteId, Context.ConnectionId);
    // Updates Sites.IsOnline = true in DB
}

// Called every 30 s by connector
public async Task Heartbeat(ConnectorHeartbeat heartbeat)
{
    // Updates Sites.LastSeenUtc in DB
}

// On disconnect
public override async Task OnDisconnectedAsync(Exception? ex)
{
    _registry.Unregister(Context.ConnectionId);
    // Updates Sites.IsOnline = false in DB
}
```

The `IConnectorRegistry` (in-memory) maps `SiteId → ConnectionId` for immediate job dispatch without a DB round-trip.

---

## 6. REST Long-Poll Fallback

When SignalR is disconnected, the connector falls back to polling:

```
GET /api/connector/jobs/poll?siteId={guid}&deviceId={PJ-PC}&waitSeconds=30
```

The API holds the request open for up to 30 s (long-poll). If a pending job arrives it is returned immediately; otherwise `{HasJob: false}` is returned after the wait.

**Authentication:** The endpoint validates that `DeviceId` matches the `SiteRecord.DeviceId` in the database. If they don't match → `null` → HTTP 404.

**Important:** The DB column `CreatedAtUtc` is `DateTimeOffset`. SQLite's EF Core provider **cannot translate** `DateTimeOffset` in `ORDER BY` clauses. The `PollNextJobAsync` query must **not** use `.OrderBy(j => j.CreatedAtUtc)`. SQLite naturally returns rows in insertion (rowid) order, which provides FIFO semantics for pending jobs.

```csharp
// Correct — no OrderBy
var job = await db.Jobs
    .Where(j => j.SiteId == siteId && j.Status == JobStatus.Pending)
    .FirstOrDefaultAsync(ct);
```

---

## 7. Job Lifecycle

```
Cloud UI / API caller
  │
  ▼
POST /api/jobs/run-and-wait
  { SiteId, Operation, Parameters, RequestedBy, IdempotencyKey }
  │
  ▼
JobService.CreateAndDispatchAsync()
  │
  ├─ creates JobRecord { Status=Pending }
  ├─ if site is online (registry has connectionId):
  │    └─ Status → Running → sends RunJob via SignalR
  └─ if site offline:
       └─ Status stays Pending → connector will pick up via REST poll
  │
  ▼
WizConnector.Service (on PC)
  receives RunJob message
  executes operation against Sage
  calls SubmitJobResult / POST /api/jobs/{id}/result
  │
  ▼
JobService.RecordResultAsync()
  Status → Completed or Failed
  pushes SignalR notification to UI (UiNotificationHub)
  │
  ▼
WaitForJobAsync() returns JobDto to original caller
```

### Job statuses

| Status | Meaning |
|--------|---------|
| `Pending` | Created, not yet dispatched to connector |
| `Running` | Dispatched, awaiting result from connector |
| `Completed` | Sage returned result JSON |
| `Failed` | Sage returned an error string |

---

## 8. Sage SDK Integration

### 8.1 Bootstrap

`SageSdkBootstrap.Initialize()` runs at service start. It:
- Resolves the SDK path from `WIZ_SAGE_SDK_PATH` env var or `SdkPath` setting.
- Calls `SetDllDirectory()` to load native Sage `.dll` files.
- Registers an `AssemblyResolve` handler for `Pastel.*`, `Evolution*`, `System.Data.SqlClient`.

This must run **before** any `Pastel.Evolution` types are referenced.

### 8.2 SageSession — thread-safe SDK access

```csharp
// SageSession holds a SemaphoreSlim(1,1) — only ONE thread in the SDK at a time.
await _session.RunAsync(async ctx =>
{
    // ctx is a DatabaseContext scoped to one company
    var customer = new CustomerMaster(ctx) { AccountCode = "C001" };
    customer.Load();
    // ... read fields ...
});
```

`SageSession` initialises `DatabaseContext` with:
- `LicenseSerial` + `LicenseKey`
- `CommonConnectionString` (master DB)
- `CompanyConnectionString` (company DB — can be an alias resolved via `Companies` dictionary)
- `AgentUser` + `AgentPassword`
- `BranchId`

### 8.3 Operation routing (`SageSdkJobExecutor`)

```
operation string
    ↓
SageSdkWriteHandlers.TryExecute()   ← write ops (gltransaction.post, etc.)
    ↓ (null = not a write)
SageSdkPhase2Handlers.TryExecute()  ← 90+ complex read handlers
    ↓ (null = not handled there)
switch (operation) in SageSdkJobExecutor:
    "customer.list"            → CustomerMaster list
    "customer.get"             → CustomerMaster by AccountCode
    "customertransaction.list" → CustomerTransaction list
    "supplier.list"            → SupplierMaster list
    "suppliertransaction.list" → SupplierTransaction list
    "glaccount.list"           → GLAccount list
    "glaccount.get"            → GLAccount by AccountCode
    "site.companies"           → SiteMetadataHandler (schema probe)
    _ → "OPERATION_NOT_SUPPORTED"
```

### 8.4 Supported read operations (partial list)

| Category | Operations |
|----------|-----------|
| Customers | `customer.list`, `customer.get`, `customer.openitems`, `customer.unpaid.summary`, `customer.aged.top` |
| Suppliers | `supplier.list`, `supplier.get`, `supplier.openitems`, `suppliertransaction.list`, `supplier.payments.top` |
| GL | `glaccount.list`, `glaccount.get`, `gltransaction.list`, `gl.period.close.readiness` |
| AR/AP | `customertransaction.list`, `customertransaction.get`, `suppliertransaction.list` |
| Sales | `salesorder.list`, `salesorder.get`, `invoice.list`, `invoice.get` |
| Inventory | `inventory.item.list`, `inventory.item.get`, `inventory.slow.moving.top`, `inventory.adjustment.top` |
| Warehouse | `warehouse.transfer.summary` |
| Treasury | `treasury.dashboard`, `bank.cashbook` |
| Dashboards | `dashboard.summary`, `vat.summary` |
| Site meta | `site.companies`, `site.metadata`, `site.schema.probe`, `site.diagnostics` |

### 8.5 Supported write operations

Write operations are gated by two controls (see §9). Each write:
1. Opens a `DatabaseContext` transaction (`BeginTran()`).
2. Creates / loads the SDK object (`GLTransaction`, `CustomerMaster`, `SalesOrder`, etc.).
3. Calls `.Post()` or `.Save()`.
4. Commits or rolls back.

| Operation | What it does |
|-----------|-------------|
| `gltransaction.post` | Post a GL journal (debit/credit must balance) |
| `customertransaction.post` | Post AR invoice or receipt |
| `suppliertransaction.post` | Post AP invoice or payment |
| `allocation.save` | Allocate a customer payment against invoices |
| `customer.save` | Create/update customer master record |
| `supplier.save` | Create/update supplier master record |
| `salesorder.save` | Create/update a sales order |
| `salesorder.confirm` | Confirm (process) a sales order |
| `salesorder.ship` | Ship a sales order |
| `purchaseorder.approve` | Approve a purchase order |
| `purchaseorder.receive` | Receive goods on a purchase order |
| `inventory.adjustment.post` | Post an inventory quantity/value adjustment |
| `warehouse.transfer.post` | Post a warehouse transfer |
| `salescreditnote.post` | Post a sales credit note |
| `suppliercreditnote.post` | Post a supplier credit note |

---

## 9. Write Safety — Two Locks Before Any Post

### 9.1 Write Consent (temporary user grant)

The Tray app menu has **"Allow cloud posts (1 hour)"**. This calls `WriteConsentHelper.Grant(TimeSpan.FromHours(1))`, which writes a timestamp to disk. `WriteConsentStore.IsAllowed()` checks whether the grant is still within its window.

If an agent attempts a write operation and consent has not been granted (or has expired), the connector returns:

```json
{ "error": "WRITE_CONSENT_REQUIRED" }
```

The web UI must surface this to the user and prompt them to open the tray and grant consent.

### 9.2 Idempotency Store

Every write job should carry an `IdempotencyKey` (UUID or hash of the payload). The `IdempotencyStore` keeps a local SQLite DB at:

```
C:\ProgramData\WizConnector\idempotency.db
```

Before executing a write:
1. If `idempotencyKey` is in the DB → return the **cached result** immediately (no Sage SDK call).
2. If not → execute → on success → save result with the key.

This prevents double-posts on network retries.

### 9.3 Two-step Approval Workflow (cloud-side)

For high-risk writes the cloud can enforce a proposal/approval flow:

```
Cloud AI/user
  → POST /api/act/propose    { Operation, Parameters, PreparedByUserId }
    → creates ApprovalProposalRecord { Status=Pending }

Approver (different user)
  → POST /api/act/approve    { ProposalId, ApprovedByUserId }
    → Status → Approved
    → triggers actual job dispatch to connector
    → creates WriteAuditRecord { BeforeJson, AfterJson, EvolutionRef }
```

`FirmRecord.IsPracticeMode = true` **blocks all writes** across every site in the firm, regardless of consent or approval. This is for demo/training environments.

---

## 10. Multi-Company Support (MC1)

A single connector can serve multiple Sage companies. The `SageSettings.Companies` dictionary maps alias → connection string:

```json
"Companies": {
  "Acme": "Server=.;Database=AcmeEvolution;Trusted_Connection=True;",
  "BetaCo": "Server=.;Database=BetaEvolution;Trusted_Connection=True;"
}
```

When a job's `Parameters` dict contains `"CompanyAlias": "Acme"`, `SageSession.ResolveCompanyConnectionString("Acme")` returns the matching connection string and opens a `DatabaseContext` for that company.

The API-side endpoint `GET /api/sites/{siteId}/companies` calls the `site.companies` operation on the connector, which runs `SiteMetadataHandler` against the resolved DB and returns:

```json
{
  "companies": ["Acme", "BetaCo"],
  "schemaCapabilities": {
    "arSupported": true,
    "apSupported": true,
    "glSupported": true,
    "invoicingSupported": true,
    "inventorySupported": false
  }
}
```

---

## 11. Online/Offline Status

The `Sites` table has `IsOnline` (bool) and `LastSeenUtc` (DateTimeOffset). The admin panel polls `GET /api/sites` to show a green/red indicator.

A site goes **Offline** when:
- `ConnectorHub.OnDisconnectedAsync()` fires (SignalR disconnect).

A site goes **Online** when:
- `RegisterSite(siteId)` is called by the connector after connecting.

`LastSeenUtc` is updated by each `Heartbeat` call (every 30 s). If `LastSeenUtc` is more than ~2 minutes old and `IsOnline = true`, the connector process has likely crashed without a clean disconnect.

---

## 12. Docker / API Configuration

The cloud API runs in a Docker container. Key settings:

| Setting | Value |
|---------|-------|
| Internal port | `8080` |
| Host port | `127.0.0.1:8088:8080` |
| DB path (inside container) | `/data/wizaccountant.db` |
| DB path (host bind-mount) | `C:/Users/pj/WizAccountant/data/wizaccountant.db` |
| Health check | TCP check: `bash -c 'exec 3<>/dev/tcp/localhost/8080 && ...'` |
| ASPNETCORE_ENVIRONMENT | `Production` |

The Docker runtime image (`mcr.microsoft.com/dotnet/aspnet:8.0`) does **not** include `wget` or `curl`. Health checks must use `bash` TCP connections or `nc`.

---

## 13. Troubleshooting for Agents

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| Site shows Offline | `connector-state.json` has stale SiteId (DB was wiped / volume changed) | Delete `C:\ProgramData\WizConnector\connector-state.json`, re-pair with a fresh code |
| REST poll returns 404 | `DeviceId` in state file doesn't match `SiteRecord.DeviceId` in DB | Re-pair; or check `appsettings.json` DeviceId |
| REST poll returns 500 | `DateTimeOffset` in `ORDER BY` in `PollNextJobAsync` | Remove the `.OrderBy()` call entirely |
| Container `(unhealthy)` | Health check uses `wget` (not in runtime image) | Change to `bash -c 'exec 3<>/dev/tcp/...'` |
| `OPERATION_NOT_SUPPORTED` | Operation string doesn't match any handler | Check spelling and casing; all operations are lowercase |
| `WRITE_CONSENT_REQUIRED` | User hasn't granted write consent via tray | Tray → "Allow cloud posts (1 hour)" |
| Sage SDK `DllNotFoundException` | `SdkPath` doesn't point to Sage Evolution install | Check `appsettings.json` `Sage.SdkPath` |
| Double-post on retry | `IdempotencyKey` not supplied in `CreateJobRequest` | Always generate a stable idempotency key per logical operation |
| SignalR disconnect loop | `ApiBaseUrl` is HTTPS but API is HTTP-only (or vice versa) | Match scheme in `pilot.config.json` and `appsettings.json` |

---

## 14. Key File Paths Reference

| File | Path | Purpose |
|------|------|---------|
| AI config | `{repo-root}/pilot.config.json` | API URL — AI-managed, highest priority |
| Connector state | `C:\ProgramData\WizConnector\connector-state.json` | `SiteId`, `TenantId`, `SiteName` after pairing |
| Sage config | `C:\ProgramData\WizConnector\sage.config` | DPAPI-encrypted Sage credentials |
| Idempotency DB | `C:\ProgramData\WizConnector\idempotency.db` | SQLite, write dedup cache |
| Service binary | `src/WizConnector.Service/bin/Release/net8.0/WizConnector.Service.exe` | Note: `net8.0` NOT `net8.0-windows` |
| Tray binary | `src/WizConnector.Tray/bin/Release/net8.0-windows/WizConnector.Tray.exe` | Windows Forms — needs `net8.0-windows` |
| Pilot (Manager) | `src/WizAccountant.Manager/bin/Release/net8.0-windows/WizPilot.exe` | Master launcher |
| SQLite DB | `C:\Users\pj\WizAccountant\data\wizaccountant.db` (host) / `/data/wizaccountant.db` (container) | Cloud API database |

---

## 15. How to Dispatch a Job (as an Agent)

### Preconditions
1. Site is paired (`SiteRecord` exists, `IsOnline = true`).
2. You have a valid JWT (from `POST /api/auth/login`).
3. For write operations: user has granted consent via tray.

### Minimal example

```http
POST /api/jobs/run-and-wait
Authorization: Bearer <jwt>
Content-Type: application/json

{
  "siteId": "68dde5ba-0529-49c6-acec-e4f1e1d6b36c",
  "operation": "customer.list",
  "parameters": {
    "take": "50",
    "skip": "0"
  },
  "requestedBy": "insight-agent",
  "idempotencyKey": "cust-list-2026-06-13-001",
  "timeoutSeconds": 60
}
```

Response (success):
```json
{
  "jobId": "...",
  "status": "Completed",
  "resultJson": "[{\"AccountCode\":\"C001\",\"Description\":\"Acme Ltd\",...}]"
}
```

### Multi-company example

Add `"CompanyAlias"` to `parameters`:

```json
{
  "operation": "customer.list",
  "parameters": {
    "CompanyAlias": "Acme",
    "take": "50"
  }
}
```

### Write example (GL journal)

```json
{
  "operation": "gltransaction.post",
  "parameters": {
    "Reference": "JNL-2026-001",
    "Description": "Monthly accrual",
    "Lines": "[{\"Account\":\"7000\",\"Debit\":1000},{\"Account\":\"2000\",\"Credit\":1000}]"
  },
  "idempotencyKey": "jnl-2026-001-unique-hash"
}
```

If `WRITE_CONSENT_REQUIRED` is returned, prompt the user to open the tray and grant consent, then retry with the same `idempotencyKey`.

---

## 16. Adding New Sage Operations

1. **Add the handler** in `SageSdkPhase2Handlers.TryExecute()` (read) or `SageSdkWriteHandlers.TryExecute()` (write). Use lowercase `operation.ToLowerInvariant()` for matching.
2. **If it's a write**, add the operation name to `ConnectorWriteAllowlist` so the consent check applies.
3. **Mock it** in `MockJobExecutor` so the API can be tested without a real Sage install.
4. **Expose an endpoint** (or use `POST /api/jobs/run-and-wait` directly) in `Program.cs`.
5. **Document the parameters** — all parameters are `Dictionary<string, string>`, so complex types (like line arrays) are JSON-encoded strings.
