# Insight AI assistant — how questions are “trained”

**Cursor training pack:** `DOCS/SAGE-AI-AGENT-TRAINING-INDEX.md`, modules in `DOCS/Sage_AI_Training/`, repo rules in `AGENTS.md`.

## Mega digest (500 common queries)

All **500** titles from `DOCS/Sage_AI_Agent_500_Common_Business_Queries_Mega_Digest.md` are embedded as `Insight/Data/mega-digest-catalog.json`.

At runtime, `MegaDigestRouter` token-matches your message to the closest catalog title and routes to an allowlisted Sage operation (or the nearest proxy). Regenerate the JSON after editing the digest:

```powershell
.\scripts\build-mega-digest-catalog.ps1
```

**Dedicated SQL (live today):** ~8 handlers covering digest items such as #1 aged debitors, #2 credit balances, #3 highest outstanding, #26 supplier aged, #51/#52 inventory GL, plus explicit chat intents (unpaid invoices, etc.).

**All 500 catalog titles** are searchable. If your wording matches a catalog entry but SQL is not built yet, Insight returns a **mega digest fallback** (not generic “try customer list” help). See `DOCS/Sage_AI_Agent_Mega_Digest_Fallback_Matcher_Patch.md`.

The Insight **AI Assistant** tab does **not** use a custom-trained language model today. It uses **safe keyword routing** in code:

| Layer | File | Role |
|--------|------|------|
| UI | `wwwroot/insight/insight.js` | Sends your message + selected site to the API |
| Router | `Insight/ChatIntentMatcher.cs` | Decides if the question matches a known pattern |
| Business process | `Insight/BusinessProcessClassifier.cs` | Semantic process before SQL routing |
| Planner | `Insight/ChatRoutePlanner.cs` | Picks one **allowlisted** Sage operation (confusion guards) |
| Chat service | `Insight/ReadOnlyChatService.cs` | Runs job, formats reply, investigation context |
| Sage | `WizConnector.Service` | Runs `CustomerTransaction.List`, etc. |

If nothing matches, you see the generic help line (*“I can help with customers, suppliers…”*) — that means **add or extend an intent**, not “train” a model.

## Aggregation mode (count / how many)

When the user asks **how many**, **count**, **number of**, or **total count**, Insight enters **aggregation mode**:

- Returns a **single number** (or short stats), **not** a transaction grid.
- Blocks mis-routed `customer.openitems` / `customer.list` / similar listing operations.
- See `DOCS/Sage_AI_Agent_Count_Query_Aggregation_Patch.md`.

## Sales invoices with discounts — count only (year)

Phrases such as:

- *how many sales invoices in 2026 have discounts*
- *total count of invoices with discount*

route to **`salesinvoice.discount.count`** — **SAGE-SALES-INV-DISC-COUNT-001** (`InvNum` + `_btblInvoiceLines`, distinct invoice count). **No grid** — count in explanation only. Not `CustomerTransaction` open items.

## Top N oldest aged debtors (AR aging)

Phrases such as:

- *list me top 5 customers with oldest aged debit balances*
- *top 10 customers by overdue aging*

route to **`customer.aged.top`** — **SAGE-AR-AGED-TOP-001** (open `CustomerTransaction` lines, ranked by **oldest invoice date**, returns **only** the requested count). Not `Customer.List`.

See **`DOCS/Sage_AI_Agent_Top5_Aged_Debtors_Patch.md`**.

## Negative stock on Balance Sheet (inventory GL credit balances)

Phrases such as:

- *do we have any negative stock values in Balance Sheet*
- *inventory accounts with negative balance*
- *stock ledgers with credit balance*

route to **`inventory.bs.negative_ledgers`** — SQL **SAGE-BS-STOCK-NEGATIVE-001** (distinct `GrpTbl.StockAccLink` accounts, `PostGL` net balance &lt; 0). This is **GL / Balance Sheet**, not physical negative quantity or SDK valuation.

See **`DOCS/Sage_AI_Negative_Stock_Balance_Sheet_Patch.md`**.

## Unpaid sales invoices (current behaviour)

Phrases such as:

- *how many sales invoices are unpaid*
- *get me sales invoices that are unpaid*
- *list unpaid sales invoices*
- *outstanding sales invoices*

route to **`customer.openitems`** with criteria **`Outstanding <> 0`** (open AR / customer transactions with a balance — the read-only proxy for unpaid invoice lines in Sage Evolution).

## How to add a new “serious” question type

1. **Confirm** the data already exists as an allowlisted operation in `InsightReadOnlyTools.cs` and the connector (`SageSdkJobExecutor` / `SageSdkPhase2Handlers`).
2. **Add a matcher** in `ChatIntentMatcher.cs` (or a new `Try…` method) with clear `Contains` / regex rules and tests for false positives (e.g. supplier vs customer).
3. **Call it early** in `ChatRoutePlanner.cs` (matcher order) — **before** broad rules like `m.Contains("customer")` → `customer.list`. Add confusion guards in `BusinessProcessConfusionGuards.cs` when a generic route competes.
4. **Tune the answer** in `FormatQueryDescription` and `FormatJobResult` so users see *Query run:* plus counts or a short row preview.
5. **Restart** the local API (WizPilot → Start local API) and **Ctrl+F5** Insight so `insight.js` and the DLL reload.

## After code changes (local pilot)

1. WizPilot → **Start local API**
2. WizPilot → **Start service + tray**
3. Insight → site **Test Local** (or your paired site) → **Ctrl+F5**
4. Retry the question in **AI Assistant**

---

## GL Period-close readiness (GAP-011)

Phrases such as:

- *is month-end ready to close*
- *are we ready to close the period*
- *can I close* / *close readiness* / *period close check*

route to **`gl.period.close.readiness`** — **SAGE-GL-PCLOSE-001**. Runs 5 SQL checks against `PostGL`:

| Check | Severity | Meaning |
|-------|----------|---------|
| Backdated postings | blocker | `TxDate` in period but `DTStamp` after period-end |
| Manual journals | warning | `PostGL.Id IN ('JL','JNL')` |
| Round-figure journals | warning | manual + round-thousand amounts |
| Unreconciled bank | blocker | `iReconciled = 0` on bank GL accounts |
| Duplicate batches | blocker | same `cAuditNumber`, COUNT > 2, non-zero balance sum |

Output: `{ readyToClose, finding, checks[], blockers[], warnings[], periodLabel }`.

---

## AP supplier payment behaviour (GAP-013)

Four operations mirror the existing AR customer payment-behaviour suite:

| Phrase | Operation |
|--------|-----------|
| *supplier payment discipline* / *how well do we pay* | `supplier.payment.behavior.summary` |
| *prompt supplier* / *who do we pay on time* | `supplier.payment.prompt.top` |
| *late supplier* / *overdue supplier* | `supplier.payment.late.top` |
| single-supplier detail | `supplier.payment.detail` (requires `supplierCode` param) |

**Data source:** `InvNum` (DocType=5) + `Vendor` — **NOT** `PostAP` (PostAP has no `InvNumKey`). Paid proxy: `Outstanding ≤ 0.01`. **PaymentDisciplineScore 0–100** (50pts paid-ratio, 20pts avg-days-overdue, 15pts zero-exposure, 15pts volume).

---

## Treasury explainability (GAP-030)

`treasury.dashboard` now answers "why is cash low" with:

- **`topContributors`** — top AR customers (inflow blockers) + top AP suppliers (outflow pressure)
- **`cashDrivers`** — `{ topArBlockers[], topApPressure[] }`
- **`likelyCause`** — deterministic string:
  - AR > 2× bank → *"Collections lagging — AR outstanding exceeds 2× bank balance"*
  - AP > bank → *"Payables pressure — AP outstanding exceeds bank balance"*
  - Projected < 0 → *"Projected cash shortfall within forecast horizon"*
  - Otherwise → *"Cash position appears stable"*

`ExplainabilityEnvelope` treats all `treasury.*` operations as explainability ops (renders `topContributors`, `likelyCause`, drilldown hint).

---

## VAT variance contributors — split by DocType (GAP-031)

`vat.variance.contributors` now returns:

| Field | Content |
|-------|---------|
| `outputVatTopContributors` | Top sales invoices by AbsVat (DocType 0/1/4) |
| `inputVatTopContributors` | Top purchase invoices by AbsVat (DocType 5) |
| `vatByCategory` | `{ standardRated, zeroRated, totalInvoices }` |
| `difference` / `reconciled` / `finding` | ReconcileEnvelope fields (backward compat) |

`OutputContractValidator` now requires both `outputVatTopContributors` and `inputVatTopContributors`.

---

## Multi-turn investigation context (GAP-014)

Entity codes mentioned in turn N are automatically available in turn N+1 without the user repeating them.

**How it works:**
1. When a job runs, entity codes in `parameters` (e.g. `customerCode=SMITH001`) are tagged into `ToolsUsedJson` as `entity:customerCode:SMITH001`.
2. On the next turn, `InvestigationContext.FromPriorAssistantMessage` parses these tags to populate `CustomerCode`, `SupplierCode`, `StockCode`, `WarehouseCode`.
3. `ApplyFollowUp` injects persisted codes into the new turn's `parameters` as fallback (current-message regex wins — explicit codes in the new message override persisted ones).

**Example:**
- Turn 1: *"Show me payment detail for supplier ACME01"* → `supplierCode=ACME01` tagged
- Turn 2: *"How about their aged balance?"* → `supplierCode=ACME01` auto-applied, no re-prompting

---

## Schema probe and connector metadata (GAP-020/021)

Two site-level operations expose schema and capability facts for the connected Sage database.

### `site.schema.probe`

Returns which of 13 core Evolution tables exist on this database and their column names.

| Parameter | Effect |
|-----------|--------|
| `tables` (optional) | Comma-separated list overriding the default 13-table probe |

Output:
```json
{ "tableCount", "tablesPresent", "tablesMissing",
  "tables": [{ "tableName", "exists", "columnCount", "columns": [...] }],
  "missingTables", "finding", "dataAsOfUtc" }
```

### `site.metadata`

Returns connector version, SDK version, and high-level capability flags derived from key-table presence.

Output:
```json
{ "connectorVersion", "sdkVersion", "companyDatabase", "handlerCount",
  "schemaProof": { "keyTableCount", "confirmedTableCount", "allKeyTablesPresent", "tables", "error" },
  "capabilities": { "arSupported", "apSupported", "glSupported", "invoicingSupported", "inventorySupported" },
  "finding", "dataAsOfUtc" }
```

Use `site.schema.probe` to diagnose "Invalid object name" errors on a customer DB.
Use `site.metadata` to confirm a site is fully operational before routing queries.

---

## Future options (not built yet)

- **LLM router**: send the user message to a model that returns only an operation name from the allowlist (still no raw SQL).
- **Intent table in DB**: edit phrases without redeploying (admin UI).
- **True invoice object**: may need extra Sage metadata (`_btblRBJoin`) and a new allowlisted operation — see `DOCS/SAGE-COMMON-RBJOIN.md`.

Until then, “training” = **adding patterns and Sage reads in this repo**, then restarting the local API.

See also **`DOCS/SAGE-200-DATABASE-LAYERS.md`** (which Sage table/layer answers which question) and **`DOCS/Sage_200_Evolution_Database_Handover.md`** (full investigation handover).
