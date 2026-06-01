# Local test — SAGE-CONSOLIDATION-001 (2026-06-09)

Use this after building/restarting the local API. Full pilot steps: `DOCS/LOCAL-PILOT-START.md`.

## 1. Build (once per code change)

From repo root in PowerShell:

```powershell
.\scripts\build-pilot-apps.ps1
dotnet build src\WizAccountant.Api\WizAccountant.Api.csproj -c Release
dotnet build src\WizAccountant.Manager\WizAccountant.Manager.csproj -c Release
```

Or in **WizPilot** → **Build pilot apps**, then **Restart local API**.

## 2. Start stack

1. **WizPilot** → Connector API URL `http://localhost:5278` → **Save URLs**
2. **Restart local API** (leave the console window open)
3. **Start service + tray** → pair site if needed
4. Open **Insight**: http://localhost:5278/insight/ → **Ctrl+F5** (hard refresh)

## 3. Confirm version

PowerShell:

```powershell
(Invoke-RestMethod http://localhost:5278/health).insightChatVersion
```

Expected: **`2026-06-09-consolidation`**

Insight header should show the same chat version (not yellow/stale).

## 4. Smoke queries (AI Assistant)

Select your paired site, then try these in order:

| # | Ask | Expected route (tools / behaviour) |
|---|-----|--------------------------------------|
| 1 | which customers pay promptly | Top prompt payers — **not** unpaid summary |
| 2 | estimate VAT payable this month | `vat.payable.estimate` |
| 3 | which items are dead stock | `inventory.nonmoving` |
| 4 | why is cash low | `treasury.dashboard` — **not** bank cashbook only |
| 5 | show negative stock on balance sheet | `inventory.bs.negative_ledgers` |
| 6 | does AR match GL | `ar.gl.reconcile` |
| 7 | top customers by outstanding balance | `customer.outstanding.debit.top` |
| 8 | why is bank reconciliation not balancing | `bank.reconcile.variance` |

## 5. Investigation follow-up (optional)

1. *why is inventory not matching GL* → explain/reconcile reply  
2. *show warehouse 10 details* → warehouse value/detail with warehouse context  
3. *show related transactions* → drilldown when stock code is in context  

## 6. Automated regression (optional)

```powershell
dotnet test tests\WizAccountant.Insight.Intents.Tests\WizAccountant.Insight.Intents.Tests.csproj
```

Expected: **1090** tests passed.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Old chat version in header | **Restart local API** + Insight **Ctrl+F5** |
| `Unsupported operation` | **Build pilot apps** + restart connector tray |
| Site offline | Admin → pairing code → tray **Pair with code** |
| Generic “I can help with customers…” | No route matched — note exact wording for a new matcher |
