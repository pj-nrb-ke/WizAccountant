# SQL Handler Architecture

## Query Pipeline

```
User message
  → SageIntentEngine.Classify()         — domain + intent + confidence
  → BusinessProcessClassifier.Classify() — semantic process type
  → InvestigationContext.ApplyFollowUp() — inject persisted entity codes (GAP-014)
  → ChatRoutePlanner.Plan()             — canonical operation + parameters
  → InsightChatPeriodHelper             — date-range gate
  → CompatibilityGate.IsCompatible()   — contract vs handler capability check
  → InsightReadOnlyTools.IsAllowed()   — allowlist gate
  → JobService.RunAndWaitAsync()        — WizConnector executes handler
  → OutputContractValidator.Validate() — shape check before formatting
  → ExplainabilityEnvelope.EnhanceReply() — enrich with topContributors, likelyCause
  → ReadOnlyChatService reply
```

## SDK vs SQL Rule

| Use SDK for | Use SQL for |
|-------------|-------------|
| Object creation / posting | Analytics & aggregation |
| Safe transactional writes | Reconciliation |
| Live balance lookups (AR/AP) | Cross-domain reporting |
| Payment discipline (InvNum+Vendor) | Period-close checks |

## Handler contract

Every connector handler implements:

```csharp
public static string Execute(string connectionString, Dictionary<string, string> parameters)
    // returns JSON string
```

Standard SQL helpers:
- `GlSqlHelper.ExecuteQuery(connectionString, sql, paramSetup)` → `List<Dictionary<string, object>>`
- `GlSqlHelper.ParseTop(parameters, default)` / `ParseHorizonDays`
- `InvNumSqlHelper.AddDateParameters(cmd, from, to)`
- `VatSqlHelper.ParsePeriod(parameters)` / `RunScalar`

## Output envelope patterns

### ReconcileEnvelope.Build
Standard for subledger vs GL reconciliations (AR, AP, VAT, Bank, FA, Inventory warehouse):
```json
{ "querySerial", "reconciliationType", "subledgerTotal", "glTotal",
  "difference", "reconciled", "matches", "finding", "topContributors", "dataAsOfUtc" }
```

### ExplainabilityEnvelope
API-side enrichment for operations containing `explain`, `variance`, `contributors`,
`reconcile`, `anomal`, `unusual`, or starting with `treasury.`:
- Renders `finding`, `topContributors` (up to 5), `likelyCause`, `confidence`, drilldown hint
- Domain drilldown hints: inventory → warehouse/item; vat → by account; bank → unmatched; treasury → AR aging / AP outstanding

### Period-close readiness shape
```json
{ "readyToClose": bool, "finding": string, "periodLabel": string,
  "checks": [{ "checkId", "status": "PASS|FAIL|WARN", "severity": "blocker|warning", "count", "description" }],
  "blockers": [...], "warnings": [...] }
```

### Treasury dashboard shape (GAP-030)
```json
{ "cashPosition", "expectedInflows", "expectedOutflows", "projectedClosingCash",
  "liquidityRatio", "likelyCause", "finding",
  "topContributors": [{ "rank", "account", "type": "inflow_blocker|outflow_pressure", "outstanding", "description" }],
  "cashDrivers": { "topArBlockers": [...], "topApPressure": [...] } }
```

### Payment discipline score (supplier/customer)
Score 0–100 per entity:
- 50 pts: paid ratio (Outstanding ≤ 0.01)
- 20 pts: avg days overdue (0 if ≤ 0 days, scales to 20)
- 15 pts: zero current overdue exposure
- 15 pts: volume (invoices paid, capped at 5)

**AP supplier source:** `InvNum (DocType=5) + Vendor` — NOT `PostAP` (PostAP has no `InvNumKey`).
**AR customer source:** `InvNum (DocType=0/4) + PostAR` (via `InvNumKey`).

## Sage table reference

| Table | Content | Key joins |
|-------|---------|-----------|
| `Client` | Customer master | `Client.DCLink = PostAR.AccountLink` |
| `Vendor` | Supplier master | `Vendor.DCLink = InvNum.AccountID` (AP) |
| `InvNum` | Invoice headers | DocType: 0=tax invoice, 1=sales CN, 3=supplier RTS, 4=POS, 5=purchase |
| `PostAR` | AR transactions | `InvNumKey` links to InvNum |
| `PostAP` | AP transactions | **No InvNumKey** — paid status via Outstanding only |
| `PostGL` | GL postings | `AccountLink`, `TxDate`, `DTStamp`, `iReconciled` |
| `Accounts` | Chart of accounts | `iAccountType`, `Description` |

## OutputContractValidator — required shapes (Phase 2 batch 2)

| Operation | Required fields |
|-----------|----------------|
| `gl.period.close.readiness` | `readyToClose`, `finding`, `checks`, `periodLabel` |
| `supplier.payment.behavior.summary` | `finding`, `promptPayers`, `slowPayers`, `averageDaysOverdue`, `suppliersAnalyzed` |
| `supplier.payment.prompt.top` | `finding`, `suppliers`, `requestedTop` |
| `supplier.payment.late.top` | `finding`, `suppliers`, `requestedTop` |
| `supplier.payment.detail` | `finding`, `supplier` |
| `treasury.dashboard` | `cashPosition`, `expectedInflows`, `expectedOutflows`, `projectedClosingCash`, `finding` |
| `vat.variance.contributors` | `difference`, `reconciled`, `finding`, `outputVatTopContributors`, `inputVatTopContributors` |

## HandlerCapabilityRegistry — capabilities per operation

Registered via `Cap(operation, domains[], metrics[], ...)`. Key flags:

| Flag | Meaning |
|------|---------|
| `dateFilter: true` | Accepts `dateFrom`/`dateTo` parameters |
| `topN: true` | Respects `top` parameter |
| `explain: true` | Supports ExplainabilityEnvelope enrichment |
| `monthlyBreakdown: true` | Supports product-by-month breakdown |
| `segmentedPeriods: true` | CompatibilityGate allows non-contiguous periods |

## Investigation context — cross-turn entity persistence (GAP-014)

Entity codes are persisted in `ToolsUsedJson` as `entity:key:value` entries:

```
"entity:customerCode:SMITH001"
"entity:supplierCode:ACME01"
"entity:stockCode:WIDGET-A"
"entity:warehouseCode:WH02"
```

`InvestigationContext.FromPriorAssistantMessage` recovers them. `ApplyFollowUp` injects them into the new turn's `parameters` as fallback. Current-message regex extractions win over persisted codes.

## SQL categories

- **AR** — `PostAR`, `Client`, `InvNum (DocType 0/1/4)`
- **AP** — `PostAP`, `Vendor`, `InvNum (DocType 5)`
- **GL** — `PostGL`, `Accounts`, `_etblGLAccountTypes`
- **Inventory** — `StkItem`, `WhseStock`, `StkMovement`, `InvNum lines`
- **VAT** — `InvNum.InvTotTax`, `PostGL` VAT control accounts
- **Bank** — `PostGL` bank accounts, `iReconciled`
- **Treasury** — SDK `CustomerTransaction` + `SupplierTransaction` + SQL bank balance
