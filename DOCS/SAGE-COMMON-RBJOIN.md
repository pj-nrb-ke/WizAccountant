# Sage common DB — `_btblRBJoin` (join metadata)

Reference for future **complex read queries** in WizAccountant. Suggested during local pilot on Evolution 11 with common database `SageCommon11`.

---

## What it is

| Item | Detail |
|------|--------|
| **Database** | Sage **common** DB (e.g. `SageCommon11`, `EvolutionCommon`) — not the company DB |
| **Table** | `_btblRBJoin` |
| **Purpose** | Stores **join definitions** Sage ships with (Report Builder / SDK relationship metadata) |
| **Use** | Discover how Sage tables link (parent/child, keys) when building multi-table reads |

WizConnector already connects to the common DB first (`CreateCommonDBConnection`) then the company DB — see `DOCS/SAGE-Connection-Process.md`.

---

## Why it matters for WizAccountant

Today the pilot uses:

- **Allowlisted SDK operations** only (`customer.list`, `supplier.list`, `inventoryitem.list`, etc.)
- **No raw SQL** from Admin, Insight, or the AI assistant (by design — security and supportability)

`_btblRBJoin` is useful **inside the connector** when we add **new composite read handlers**, for example:

- Customer + open items + last payment in one job
- Inventory + warehouse + valuation in one job
- GL + sub-ledger joins for dashboard KPIs

Instead of guessing table relationships, we can **look up Sage’s own join rows** and build criteria or SDK paths that match Evolution’s model.

---

## Example (explore in SSMS)

Connect to **SageCommon11** (same server as setup):

```sql
-- Inspect structure (column names vary slightly by Evolution version)
SELECT TOP 50 *
FROM _btblRBJoin
ORDER BY 1;
```

Use this to find joins involving tables you care about (e.g. Client, InvNum, StkItem):

```sql
-- Example pattern — adjust column names after inspecting TOP 50 *
SELECT *
FROM _btblRBJoin
WHERE /* join or table name columns */ LIKE '%Client%'
   OR /* ... */ LIKE '%StkItem%';
```

Always treat results as **metadata**, not business data. Business rows stay in the **company** database.

---

## Recommended usage in WizAccountant

| Phase | Approach |
|-------|----------|
| **Now (pilot)** | Document only; keep using allowlisted `*.list` / `*.openitems` operations |
| **Next reads** | For each new composite feature: query `_btblRBJoin` once (dev), design a **named operation** (e.g. `customer.balanceDetail`), implement via SDK or controlled SQL in connector only |
| **Never** | Expose arbitrary SQL or dynamic joins to the web UI or AI chat |

Flow for a new complex read:

1. Find join path in `_btblRBJoin` (common DB).
2. Prototype SQL or SDK list in SSMS / small connector test.
3. Add operation to `InsightReadOnlyTools.Allowed` + `SageSdkJobExecutor` / phase-2 handlers.
4. Expose in Insight tab or AI intent (e.g. “customer balance detail”).

---

## Connection requirements

| Setting | Where |
|---------|--------|
| Common DB name | WizPilot → **Open Sage setup** → Common database = `SageCommon11` (or your site’s name) |
| SQL access | Same SQL login as setup **Check** — must read common DB |
| Runtime | `Sage:CommonConnectionString` loaded from encrypted `sage.config` |

---

## Related tables (common DB)

Other common-DB metadata tables exist on some versions (names vary). Treat `_btblRBJoin` as the primary **join catalogue** unless Sage docs for your version point elsewhere.

---

## See also

- `DOCS/SAGE-Connection-Process.md` — connection order, common + company DB
- `DOCS/PHASE2-AI-GUARDRAILS.md` — AI allowlist, no raw SQL from chat
- `src/WizAccountant.Api/Insight/InsightReadOnlyTools.cs` — allowed read operations

---

*Added from pilot feedback — use for designing composite Sage reads, not for end-user SQL.*
