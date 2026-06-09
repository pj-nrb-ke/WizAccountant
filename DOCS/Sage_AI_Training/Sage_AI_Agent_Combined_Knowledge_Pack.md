# Sage 200 Evolution AI Agent Knowledge Pack

## 1. Purpose

This knowledge pack is for building an AI agent that can answer business questions from a Sage 200 Evolution database using natural-language chat.

The agent must not behave like a simple SDK wrapper or generic SQL generator. Sage 200 Evolution business questions often require joining multiple functional areas such as Customers, Invoices, Payments, Inventory, GL, Manufacturing, Fixed Assets, Cost Tracking, and Periods.

The correct design is a **hybrid Sage AI Agent**:

1. Use the Sage SDK for standard safe object-level operations where it is reliable.
2. Use direct SQL read-only queries for cross-domain analytical questions.
3. Use direct SQL diagnostic queries for reconciliation and investigation.
4. Use controlled transaction-based datafix scripts only when explicitly approved.

## 2. Important Design Principle: Go Beyond SDK

Cursor must understand this clearly:

> The Sage SDK is not enough for analytical, cross-domain, reconciliation, and historical investigation questions.

The SDK is useful for standard business object access, but it often struggles with questions like:

- “Show all customers with open invoices grouped by customer.”
- “Show invoices, payments, allocations, and outstanding balances.”
- “Why does Balance Sheet inventory not match Inventory Valuation?”
- “Which BOM component posted to the wrong GL account?”
- “Why did depreciation change between months?”
- “Which source table supports the GL posting?”

For these questions, the agent must use direct database querying with a Sage semantic layer.

### SDK-first but SQL-capable rule

The agent should follow this decision flow:

```text
User question
  ↓
Classify intent and domain
  ↓
Can SDK answer the full question accurately?
  ├─ Yes → use SDK
  └─ No → use read-only SQL through approved query templates
```

### Do not force everything through SDK

Do not attempt to answer cross-domain questions by pulling disconnected SDK objects and guessing relationships in application code. That causes incomplete or misleading answers.

For Sage investigation work, SQL is required because the source of truth often lives in posting tables such as:

- `PostGL`
- `PostST`
- `PostAR`
- `PostAP`
- `_etblInvCostTracking`
- `_btblFAGLBatchAssetValues`
- `_btblFAGLTotalAssetValues`
- Manufacturing process tables
- GL period tables

## 3. Agent Architecture

The Sage AI Agent should use this architecture.

```text
Natural Language User Query
        ↓
Intent Classifier
        ↓
Domain Router
        ↓
Sage Semantic Layer
        ↓
Query Planner
        ↓
SDK or SQL Decision
        ↓
Safe Query Executor
        ↓
Result Interpreter
        ↓
Business Answer
```

## 4. Intent Classification

Every user question must first be classified before SQL generation.

The agent should classify into:

| Field | Meaning |
|---|---|
| Domain | AR, AP, Inventory, GL, Manufacturing, Fixed Assets, Sales, Purchases, Unknown |
| Intent | What the user wants, e.g. open invoices, reconciliation, movement history |
| Entities | Customers, invoices, payments, items, accounts, assets |
| Source of Truth | Which Sage table or view should be trusted |
| Output Shape | Detail list, grouped summary, reconciliation, exception list |
| Risk Level | read_only, diagnostic, datafix_preview, datafix_live |

Example:

```json
{
  "domain": "Accounts Receivable",
  "intent": "Open customer invoices",
  "entities": ["customers", "invoices", "payments", "allocations"],
  "source_of_truth": ["Client", "InvNum", "PostAR"],
  "output_shape": "Customer grouped invoice detail with outstanding amount",
  "risk_level": "read_only"
}
```

## 5. Risk Modes

The agent must never run unsafe SQL without explicit approval.

| Mode | Allowed SQL | Purpose |
|---|---|---|
| read_only | SELECT only | Normal chat queries |
| diagnostic | SELECT, CTE, temp tables | Investigation and reconciliation |
| datafix_preview | BEGIN TRAN + INSERT/UPDATE + ROLLBACK | Test datafix |
| datafix_live | COMMIT only after user approval | Live correction |

Mandatory rule:

```text
For datafix scripts, always preview with ROLLBACK first.
Never commit automatically.
```

## 6. Core Sage Tables and Meaning

### 6.1 GL and Financial Posting Tables

| Table | Meaning | Usage |
|---|---|---|
| `PostGL` | Main General Ledger posting table | Balance Sheet, P&L, audit trail, account movements |
| `Accounts` | GL account master | Account code, description, account type |
| `Period` | Financial period table | Used to populate `PostGL.[Period]` |
| `_etblGLAccountTypes` | GL account type definitions | Used to classify Balance Sheet/P&L account types |

Important fields in `PostGL`:

| Field | Meaning |
|---|---|
| `TxDate` | Transaction date |
| `[Period]` | Sage financial period number |
| `Id` | Transaction source type, e.g. `OInv`, `JL`, `MFDR`, `MFMF`, `IJr` |
| `TrCodeID` | Transaction code id |
| `AccountLink` | GL account affected |
| `Debit` | Debit amount |
| `Credit` | Credit amount |
| `Description` | Transaction description or asset/item class |
| `Reference` | Reference/document number |
| `cAuditNumber` | Audit batch number linking related postings |
| `Project` | Project/cost center indicator |
| `UserName` | User who posted |
| `DTStamp` | Posting timestamp |

GL net value convention:

```sql
NetValue = ISNULL(Debit, 0) - ISNULL(Credit, 0)
```

## 7. Inventory and Stock Tables

| Table | Meaning | Usage |
|---|---|---|
| `StkItem` | Stock item master | Item code, description, stock/service flag |
| `_etblStockDetails` | Item group/warehouse details | Links item and warehouse to stock group |
| `GrpTbl` | Stock group master | GL mappings for sales, COS, stock, purchases, WIP |
| `PostST` | Stock transaction posting table | Stock movements and item-level posting audit |
| `_etblInvCostTracking` | Cost tracking table | As-at valuation cost source |
| `_evInvCostTracking` | Sage valuation helper/view | Used in Sage valuation report logic |
| `_bvSTTransactionsFull` | Stock transaction view | Used to calculate as-at quantities in valuation logic |

Key relationships:

```text
StkItem.StockLink = PostST.AccountLink
StkItem.StockLink = _etblStockDetails.StockID
_etblStockDetails.GroupID = GrpTbl.idGrpTbl
GrpTbl.StockAccLink = Accounts.AccountLink
GrpTbl.COSAccLink = Accounts.AccountLink
GrpTbl.SalesAccLink = Accounts.AccountLink
GrpTbl.iWIPAccID = Accounts.AccountLink
_etblInvCostTracking.iStockID = StkItem.StockLink
_etblInvCostTracking.iWarehouseID = _etblStockDetails.WhseID
```

### Important Inventory Rule

For Sage Inventory Valuation by Date:

```text
Do not use PostST as the valuation cost source.
Use Sage cost tracking / valuation logic.
```

`PostST` is excellent for movement audit, but it is not the correct source for as-at valuation cost.

## 8. Manufacturing and BOM Tables

Important manufacturing concepts:

| Posting Type | Meaning |
|---|---|
| `MFDR` | Component drawn into manufacturing/WIP |
| `MFR4M` | Component used from WIP into manufactured item |
| `MFMF` | Finished item manufactured |
| `WTrf` | Warehouse transfer |
| `OInv` | Sales invoice stock movement |
| `IJr` | Inventory journal / adjustment |

Typical manufacturing flow:

```text
Component draw:
Dr WIP
Cr Component source account

Component used:
Cr WIP

Finished good creation:
Dr Finished Goods Inventory

Sale:
Dr Cost of Sales
Cr Finished Goods Inventory
```

Key manufacturing relationships used during investigation:

```text
PostST.iMFPID / PostST.iMFPLineID relate to manufacturing process activity
PostST.cAuditNumber links to PostGL.cAuditNumber
PostGL.cAuditNumber groups the GL postings of a manufacturing event
Manufacturing process tables contain process reference, finished item, component item, quantities, costs
```

Known useful manufacturing tables from investigation:

| Table | Meaning |
|---|---|
| `_etblManufProcess` | Manufacturing process header |
| `_etblManufProcessItem` | Process item/component lines |
| `_etblManufProcessLine` | Process transaction line values |

## 9. Fixed Asset Tables

| Table | Meaning |
|---|---|
| `_btblFAAsset` | Fixed asset master |
| `_btblFAAssetType` | Asset type and GL mapping |
| `_btblFAGLBatchAssetValues` | Asset-level depreciation values by batch/date |
| `_btblFAGLTotalAssetValues` | Asset-level total depreciation values |
| `PostGL` | Actual GL depreciation posting |

Key relationships:

```text
_btblFAAsset.idAssetNo = _btblFAGLBatchAssetValues.iAssetID
_btblFAAsset.idAssetNo = _btblFAGLTotalAssetValues.iAssetID
_btblFAAsset.iAssetTypeNo = _btblFAAssetType.idAssetTypeNo
_btblFAAssetType.iCreditGLAccountID = accumulated depreciation account
_btblFAAssetType.iGLAccountNo = depreciation expense account
```

Important Fixed Asset rule:

```text
For depreciation source-of-truth, compare PostGL to _btblFAGLBatchAssetValues.
```

This was proven during the Jan/Feb/March depreciation issue. March was not wrong; Jan and Feb GL postings were overstated compared to FA batch source values.

## 10. Source-of-Truth Rules

| Business Question | Source of Truth |
|---|---|
| GL balance | `PostGL` |
| Balance Sheet account balance | `PostGL` grouped by `Accounts` |
| Stock movement audit | `PostST` |
| Inventory valuation by date | Sage valuation logic using cost tracking |
| Inventory vs GL mismatch | Valuation source vs `PostGL` inventory accounts |
| BOM component ledger impact | `PostST` + `PostGL` joined by `cAuditNumber` |
| Fixed asset depreciation source | `_btblFAGLBatchAssetValues` |
| Fixed asset depreciation posted to GL | `PostGL` |
| Customer balance | AR ledger / customer transaction source |
| Open invoice detail | Invoice + AR transactions/payments/allocations |

## 11. Accounts Receivable Semantic Layer

The agent must handle AR questions beyond simple customer balances.

### Business meaning

Open invoices means:

```text
Invoice amount - receipts - credit notes - allocations = outstanding amount
```

A customer with a balance greater than a number is not the same as listing open invoices.

### Typical AR entities

| Entity | Meaning |
|---|---|
| Customer master | Customer code, name, terms, contact |
| Invoice header | Invoice number, invoice date, customer, total |
| AR postings | Invoice, payment, credit note, allocation ledger |
| Payment records | Receipts and allocations |
| Ageing buckets | Current, 30, 60, 90, 120+ days |

### Desired output for open invoice query

When user asks:

> Get me all customers who have open invoices.

The agent should produce:

| Customer Code | Customer Name | Invoice Number | Invoice Date | Due Date | Invoice Amount | Paid/Allocated | Outstanding | Ageing Bucket |
|---|---|---|---|---|---:|---:|---:|---|

### AR query plan pattern

```text
1. Identify customer table.
2. Identify invoice table.
3. Identify AR transaction/payment/allocation table.
4. Calculate outstanding amount at invoice level.
5. Filter outstanding amount > 0.
6. Group/order by customer and due date.
7. Produce customer subtotals and grand total.
```

### AR guardrail

The agent must not answer open invoice questions with only:

```sql
Customer balance > 0
```

That is insufficient.

## 12. Inventory Valuation vs Balance Sheet Pattern

### Business question

> Why does Balance Sheet inventory not match Inventory Valuation?

### Correct reasoning

```text
Balance Sheet inventory = PostGL inventory account balances
Inventory Valuation = Sage valuation report logic / cost tracking
Difference must be isolated by stock group and mapped inventory GL account
```

### Key relationship

```text
StkItem → _etblStockDetails → GrpTbl.StockAccLink → Accounts
```

### Lessons learned

- `PostGL` matched Balance Sheet.
- Sage Inventory Valuation was correct.
- Mismatch was isolated to `NEWRM01 / Packaging Materials`.
- Root cause was opening migration mismatch between Cost Tracking opening value and GL/PostST opening value.
- Datafix was GL-only: debit inventory, credit stock adjustment.

## 13. Water BOM / DSTK Pattern

### Business question

> DSTK is a service component representing KRA Excise payment. Why is it not correctly hitting Cost of Sales?

### Correct finding

DSTK was flowing into finished goods cost and eventually Cost of Sales, but its source ledger was wrong.

Wrong setup:

```text
DSTK ServiceItem = 1
DSTK mapped to NEWRM01 / Packaging Materials
NEWRM01 StockAccLink = 1225 / Inventory - Packaging Materials
```

Wrong posting:

```text
Cr 1225 Inventory - Packaging Materials
Dr WIP
Cr WIP
Dr Finished Goods Inventory
Dr COS when sold
```

Correct setup:

```text
DSTK should be mapped to NEWS007 / KRA Excise / Stamps Cost
Stock/COS/Purchase/WIP GL = 1191 / 4890 / Stamp Duties
```

Historical datafix:

```text
Update PostST DSTK MFDR rows from iGLAccountID 1225 to 1191
Update matching PostGL DSTK drawn rows from AccountLink 1225 to 1191
Leave WIP, finished goods, and COS rows unchanged
```

## 14. Fixed Asset Depreciation Pattern

### Business question

> Jan and Feb depreciation were consistent, then March reduced. No asset was bought or disposed.

### Correct finding

March was correct. Jan and Feb GL depreciation were overstated.

Correct comparison:

```text
PostGL depreciation postings vs _btblFAGLBatchAssetValues by asset type/date
```

Datafix direction:

```text
Debit accumulated depreciation
Credit depreciation expense
Only for excess Jan/Feb depreciation
Do not touch March
```

## 15. Query Planning Rules for the AI Agent

Before writing SQL, always output an internal plan with these steps:

```text
1. Identify domain.
2. Identify business meaning.
3. Identify source-of-truth tables.
4. Identify required joins.
5. Identify filters/date range.
6. Identify aggregation level.
7. Identify expected output columns.
8. Identify validation checks.
9. Generate SQL.
10. Interpret results in business language.
```

The user does not always need to see the full internal plan, but the agent should follow it.

## 16. Natural Language Examples and Expected Behavior

### Example 1

User:

```text
Get me all customers who have open invoices.
```

Agent should not do:

```text
SELECT customers where balance > 0
```

Agent should do:

```text
Find invoice-level outstanding balances after payments/allocations, group by customer, show invoice detail and customer totals.
```

### Example 2

User:

```text
Why is inventory valuation not matching Balance Sheet?
```

Agent should do:

```text
Compare PostGL inventory accounts to Sage valuation logic grouped by stock group and GL account.
```

### Example 3

User:

```text
Why is March depreciation lower than January and February?
```

Agent should do:

```text
Compare PostGL monthly depreciation to FA batch source values by asset type and asset.
```

### Example 4

User:

```text
Show DSTK impact in water BOM.
```

Agent should do:

```text
Find DSTK in manufacturing process lines, PostST, PostGL, and stock group GL mapping.
```

## 17. SQL Safety Rules

The agent must enforce these rules:

1. Default to `SELECT`.
2. No `UPDATE`, `INSERT`, `DELETE`, or `COMMIT` unless user explicitly asks for datafix.
3. All datafixes must first run with:

```sql
BEGIN TRANSACTION;
-- correction
ROLLBACK TRANSACTION;
```

4. Live datafix should be separate and only after:
   - Backup confirmation
   - User approval
   - Test output reviewed
5. Always include a unique query serial number.

Serial format examples:

```text
SAGE-AR-OPEN-001
SAGE-INVVAL-001
SAGE-WATER-BOM-001
SAGE-FA-DEP-001
SAGE-LIVE-FIX-001
```

## 18. Cursor Implementation Instructions

Cursor should implement this as project files:

```text
/knowledge/sage_200_semantic_layer.md
/knowledge/sage_query_patterns.md
/agent/intent_classifier.ts
/agent/query_planner.ts
/agent/sql_generator.ts
/agent/safety_guard.ts
/agent/result_interpreter.ts
/sql_templates/accounts_receivable.sql
/sql_templates/inventory.sql
/sql_templates/manufacturing.sql
/sql_templates/fixed_assets.sql
/sql_templates/general_ledger.sql
```

### Agent operating rule

```text
The agent must never answer directly from SDK results if the question requires cross-domain reconciliation.
It must switch to direct SQL read-only templates.
```

## 19. Required Query Pattern Library

The agent should include these reusable templates.

### AR patterns

- Open invoices by customer
- Customer statement
- Customer ageing
- Payments received by customer
- Unallocated receipts
- Invoice payment allocation trace

### Inventory patterns

- Item stock movement
- Inventory valuation by date
- Inventory by stock group
- Inventory vs GL account
- Cost tracking by item/warehouse
- Wrong GL postings by stock group

### Manufacturing patterns

- BOM/component usage
- Manufacturing process GL impact
- Component to WIP to finished goods trace
- Service item mapped to inventory account detection

### Fixed Asset patterns

- Depreciation by month/type
- GL depreciation vs FA batch source
- Asset-level depreciation movement
- Accumulated depreciation reconciliation

### GL patterns

- Account balance by date
- Account movement by period
- Audit number drilldown
- Journal batch validation
- Balance Sheet account grouping

## 20. Output Style for the AI Agent

The agent should answer in this format:

```text
Finding:
[Short business finding]

Evidence:
[Key totals/counts]

Likely Cause:
[Cause based on Sage tables]

Next Step:
[One recommended query or action]
```

Avoid long explanations unless the user asks.

## 21. Minimum Viable Task for Cursor

The first implementation milestone should be:

> Build a working read-only Sage AI agent that can answer: “Get me all customers with open invoices grouped by customer.”

The agent must:

1. Classify intent as AR/Open Invoices.
2. Use direct SQL if SDK cannot provide invoice-level outstanding balances.
3. Return customer grouped output.
4. Provide invoice-level details.
5. Summarize total outstanding.
6. Avoid data modification.

## 22. Final Rule

The Sage AI Agent must treat Sage as a business accounting system, not just a database.

Correct answers require:

```text
Business meaning + Sage table relationships + source-of-truth rules + safe SQL execution
```

This knowledge pack should be loaded into Cursor as a permanent instruction file and referenced by every Sage query workflow.




---

# Sage AI Agent Knowledge Pack — Fixed Assets

## Purpose
This pack teaches the Sage AI Agent how to answer Fixed Asset questions, especially depreciation, accumulated depreciation, asset class, and GL reconciliation issues.

## Typical User Questions
- Show fixed asset depreciation by month.
- Why did depreciation change in March?
- Show depreciation by asset class.
- Show asset-level depreciation.
- Compare fixed asset register with GL.
- Show assets purchased/disposed in a period.
- Reconcile depreciation expense to FA batch values.
- Reconcile accumulated depreciation by asset type.

## Core Tables
| Table | Meaning |
|---|---|
| `_btblFAAsset` | Fixed asset master |
| `_btblFAAssetType` | Asset type and GL mapping |
| `_btblFAGLBatchAssetValues` | Asset-level depreciation values by batch/date |
| `_btblFAGLTotalAssetValues` | Asset-level total depreciation values |
| `PostGL` | Actual GL postings |
| `Accounts` | GL account master |

## Key Relationships
```text
_btblFAAsset.idAssetNo = _btblFAGLBatchAssetValues.iAssetID
_btblFAAsset.idAssetNo = _btblFAGLTotalAssetValues.iAssetID
_btblFAAsset.iAssetTypeNo = _btblFAAssetType.idAssetTypeNo
_btblFAAssetType.iCreditGLAccountID = accumulated depreciation account
_btblFAAssetType.iGLAccountNo = depreciation expense account
PostGL.Description often stores asset type code during FA postings
```

## Source-of-Truth Rules
| Question | Correct Source |
|---|---|
| Asset master | `_btblFAAsset` |
| Asset type and GL mapping | `_btblFAAssetType` |
| Depreciation source amount | `_btblFAGLBatchAssetValues` |
| GL posted depreciation | `PostGL` |
| Depreciation reconciliation | Compare `_btblFAGLBatchAssetValues` vs `PostGL` |

## Important Lesson
If depreciation appears lower in one month, do not assume that lower month is wrong.

Correct method:
```text
Compare PostGL monthly depreciation against _btblFAGLBatchAssetValues by asset type/date.
```

## Diagnostic Pattern — Monthly Depreciation Drop
1. Summarize `PostGL` depreciation by month and asset type.
2. Summarize `_btblFAGLBatchAssetValues` by month and asset type.
3. Compare GL vs FA batch.
4. If GL differs but FA batch is consistent, GL posting is wrong.
5. If FA batch itself differs, inspect asset-level FA batch values.
6. Check asset purchases/disposals only after source comparison.

## Output Shape — Depreciation Reconciliation
| DepDate | Asset Type | GL Depreciation | FA Batch Depreciation | Difference |
|---|---|---:|---:|---:|

## Datafix Direction
If GL is overstated:
```text
Dr Accumulated Depreciation
Cr Depreciation Expense
```

If GL is understated:
```text
Dr Depreciation Expense
Cr Accumulated Depreciation
```

## Guardrails
- Do not update FA source tables unless proven wrong.
- Prefer balanced GL correction if FA batch source is correct.
- Do not touch months already matching FA batch.
- Always preserve period and audit trail.




---

# Sage AI Agent Knowledge Pack — Inventory / Stock / Valuation

## Purpose
This pack teaches the Sage AI Agent how to answer inventory, stock movement, stock valuation, warehouse, stock group, and GL reconciliation questions in Sage 200 Evolution.

## Core Principle
Inventory in Sage has multiple layers:
- Item master
- Warehouse stock details
- Stock group GL mapping
- Stock movement postings
- Cost tracking / valuation
- General Ledger postings

The agent must choose the correct source depending on the question.

## Typical User Questions
- Show stock balance for item X.
- Show stock movement history for item X.
- Why is inventory valuation not matching Balance Sheet?
- Show inventory value by stock group.
- Show stock items with negative quantity.
- Show items posted to wrong GL account.
- Show stock group GL mapping.
- Show inventory adjustment transactions.
- Show warehouse transfer history.
- Trace item from purchase/manufacturing to sale.

## Core Tables
| Table | Meaning |
|---|---|
| `StkItem` | Stock item master |
| `_etblStockDetails` | Item warehouse/group details |
| `GrpTbl` | Stock group and GL mapping |
| `PostST` | Stock transaction postings |
| `PostGL` | GL postings |
| `Accounts` | GL account master |
| `_etblInvCostTracking` | Cost tracking / valuation cost source |
| `_evInvCostTracking` | Sage valuation helper/view |
| `_bvSTTransactionsFull` | Full stock transaction view |

## Key Relationships
```text
StkItem.StockLink = PostST.AccountLink
StkItem.StockLink = _etblStockDetails.StockID
_etblStockDetails.GroupID = GrpTbl.idGrpTbl
GrpTbl.StockAccLink = Accounts.AccountLink
GrpTbl.COSAccLink = Accounts.AccountLink
GrpTbl.SalesAccLink = Accounts.AccountLink
GrpTbl.iWIPAccID = Accounts.AccountLink
_etblInvCostTracking.iStockID = StkItem.StockLink
_etblInvCostTracking.iWarehouseID = _etblStockDetails.WhseID
PostST.cAuditNumber = PostGL.cAuditNumber
```

## Source-of-Truth Rules
| Question | Correct Source |
|---|---|
| Stock movement | `PostST` |
| GL inventory balance | `PostGL` |
| Inventory valuation by date | Sage valuation / cost tracking logic |
| Stock group GL mapping | `GrpTbl` |
| Wrong stock GL posting | `PostST.iGLAccountID` and `PostGL.AccountLink` |
| Inventory vs GL mismatch | valuation logic vs `PostGL` inventory account |

## Important Rule
Do not use `PostST` as the cost source for Inventory Valuation by Date.

## Inventory Valuation Logic Concept
Sage valuation uses:
```text
Item + Warehouse + latest valid cost tracking as at date + quantity balance logic
```

## Common Reconciliation Pattern
To compare Inventory Valuation vs Balance Sheet:
1. Calculate Balance Sheet inventory from `PostGL`.
2. Calculate valuation from Sage valuation logic.
3. Group valuation by `GrpTbl.StockAccLink`.
4. Compare by GL AccountLink.
5. Drill down by stock group.
6. Drill down by item/warehouse.
7. Check opening migration, wrong-account postings, stock group changes, and service items mapped to inventory.

## Known Diagnostic Pattern — Wrong GL Posting
```text
Stock item → PostST.iGLAccountID
Expected account → GrpTbl.StockAccLink
If different, investigate historical group mapping or incorrect posting.
```

## Output Shape — Inventory vs GL
| Stock Group | Stock Group Name | Inventory GL | Valuation | GL Balance | Difference |
|---|---|---|---:|---:|---:|

## Guardrails
- Always separate stock movement from valuation.
- Always include warehouse where valuation/quantity matters.
- Do not modify cost tracking without explicit approval.
- For datafix, prefer balanced PostGL correction unless valuation source itself is wrong.




---

# Sage AI Agent Knowledge Pack — Suppliers / Accounts Payable

## Purpose
This pack teaches the Sage AI Agent how to answer supplier and Accounts Payable questions that require supplier master data, purchase invoices, supplier payments, allocations, ageing, and GL drill-down.

## Core Principle
Supplier queries must not be answered only from supplier balance fields. For open invoices, ageing, payment allocation, and supplier statements, the agent must work at document/transaction level.

## Typical User Questions
- Show all suppliers with open invoices.
- Show unpaid supplier invoices grouped by supplier.
- Show supplier ageing as of a specific date.
- Show all payments made to a supplier last month.
- Which supplier invoices are overdue?
- Which supplier invoices are partly paid?
- Show supplier statement for a date range.
- Show unallocated supplier payments.
- Why does supplier control account not match AP ageing?

## Domain Classification
```json
{
  "domain": "Accounts Payable",
  "intent": "Supplier open invoices / ageing / statement / payment trace",
  "risk_level": "read_only",
  "source_preference": "Direct SQL for cross-domain analysis; SDK only for simple supplier master lookup"
}
```

## Source-of-Truth Rules
| Question | Source of Truth |
|---|---|
| Supplier list | Supplier master table / SDK acceptable |
| Supplier balance summary | Supplier master balance or AP ledger summary |
| Open supplier invoices | AP transaction detail, invoices, payments, allocations |
| Supplier ageing | AP transaction detail as of date |
| Payment trace | AP postings and payment allocation records |
| AP control reconciliation | PostGL supplier control account vs AP subledger |

## Required Query Thinking
When the user asks for supplier open invoices:
1. Identify supplier.
2. Identify supplier invoice transactions.
3. Identify supplier payments, credit notes, and allocations.
4. Calculate invoice-level outstanding amount.
5. Filter outstanding amount > 0.
6. Group by supplier.
7. Age by invoice date or due date.

## Output Shape — Open Supplier Invoices
| Supplier Code | Supplier Name | Invoice No | Invoice Date | Due Date | Invoice Amount | Paid / Allocated | Outstanding | Ageing Bucket |
|---|---|---|---|---|---:|---:|---:|---|

## Guardrails
- Do not answer “open invoices” by only checking supplier balance.
- Do not mix supplier statement with GL control account unless reconciling.
- Do not assume payments are fully allocated.
- For datafix, require preview mode with `ROLLBACK`.

## SQL Pattern Placeholder
Cursor must map actual Sage AP tables from the target database schema. Likely tables include supplier master, purchase invoice header, AP posting/allocation tables, and PostGL control postings.

Minimum SQL planning fields:
```text
Supplier master table
Purchase invoice table
AP transaction table
Payment/allocation table
GL supplier control account
Period table
```

## Recommended Agent Response Format
```text
Finding:
[Supplier/AP finding]

Evidence:
[Total suppliers, invoices, outstanding value]

Likely Cause:
[If reconciliation issue]

Next Step:
[One query/action]
```




---

# Sage AI Agent Knowledge Pack — Natural Language Transaction Processing

## Purpose
This pack defines how the Sage AI Agent should process business transactions from plain English, such as invoices, receipts, supplier invoices, stock adjustments, and journals.

This is higher risk than read-only querying. The agent must be strict, structured, and approval-driven.

## Core Principle
Natural language transaction processing must not directly post to Sage from one prompt.

The correct flow is:

```text
User natural language request
  ↓
Intent classification
  ↓
Extract transaction draft
  ↓
Validate master data
  ↓
Preview transaction
  ↓
Ask user for confirmation
  ↓
Post through Sage SDK if supported
  ↓
Verify posting from Sage database
```

## SDK vs SQL Rule
For transaction creation, prefer Sage SDK or Sage-supported business object APIs.

Direct SQL inserts into transaction tables should be avoided unless:
- There is no SDK option.
- Sage business logic impact is fully understood.
- User explicitly approves.
- Full backup exists.
- Transaction is previewed with rollback.
- Post-validation queries are available.

## Supported Transaction Types

### Phase 1 — Recommended
| Transaction | Recommended Route |
|---|---|
| Sales invoice | SDK/business object |
| Sales order | SDK/business object |
| Customer receipt | SDK/business object |
| Supplier invoice | SDK/business object |
| Supplier payment | SDK/business object |
| Stock adjustment | SDK/business object if available |
| Journal entry | SDK or controlled PostGL insertion only if approved |

### Phase 2
| Transaction | Route |
|---|---|
| Manufacturing/BOM processing | SDK or Sage UI-assisted workflow |
| Fixed asset depreciation | Sage FA module, not direct SQL |
| Inventory valuation correction | Diagnostic + approved datafix only |

## Natural Language Example

User:
```text
Process an invoice for customer ABC Ltd for item A qty 10 at 250, item B qty 5 at 100, VAT applicable.
```

Agent must extract:

```json
{
  "transaction_type": "sales_invoice",
  "customer": "ABC Ltd",
  "document_date": "today",
  "currency": "company_default",
  "lines": [
    {"item": "A", "quantity": 10, "unit_price": 250, "tax": "VAT"},
    {"item": "B", "quantity": 5, "unit_price": 100, "tax": "VAT"}
  ],
  "posting_mode": "draft_preview"
}
```

## Mandatory Validation Before Posting

For sales invoices:
1. Customer exists and is active.
2. Customer account is not on hold.
3. Item codes exist and are active.
4. Stock availability is checked for stocked items.
5. Unit prices are confirmed.
6. VAT/tax code is confirmed.
7. Warehouse is confirmed.
8. Posting period is open.
9. Document total is calculated and shown.
10. User explicitly confirms posting.

## Transaction Preview Output
Before posting, show:

| Field | Value |
|---|---|
| Customer | Customer code/name |
| Date | Invoice date |
| Warehouse | Warehouse |
| Currency | Currency |
| VAT | Tax treatment |
| Net Total | Amount |
| VAT Total | Amount |
| Gross Total | Amount |

Line preview:

| Item | Description | Qty | Unit Price | Net | VAT | Gross |
|---|---|---:|---:|---:|---:|---:|

User confirmation wording required:

```text
Please confirm: POST THIS INVOICE
```

Do not post if user says vague words like “ok” or “fine” for high-risk transactions.

## Safety Rules

### Never do this
- Never post invoice directly from ambiguous prompt.
- Never guess customer if multiple matches exist.
- Never guess item if multiple matches exist.
- Never use direct SQL for normal invoice posting unless no supported route exists.
- Never silently post to closed period.
- Never ignore VAT.

### Always do this
- Validate all master data.
- Generate preview.
- Ask for explicit confirmation.
- Post using SDK/business object.
- Verify with read-only SQL after posting.
- Return document number and audit trail.

## Agent Transaction Modes

| Mode | Meaning |
|---|---|
| draft_only | Prepare transaction preview only |
| validate_only | Validate customer/items/prices/stock |
| post_after_confirmation | Post only after explicit confirmation |
| diagnostic | Inspect posting after creation |
| datafix | Requires special approval |

## Processing Flow for Sales Invoice

```text
1. Parse user instruction.
2. Identify customer.
3. Identify items.
4. Validate quantities and prices.
5. Determine warehouse.
6. Determine tax treatment.
7. Generate invoice preview.
8. Ask for explicit confirmation.
9. Post through SDK.
10. Read back invoice from Sage.
11. Verify PostGL/PostST if required.
12. Summarize result.
```

## Post-Posting Verification

After invoice posting, verify:
- Invoice header exists.
- Invoice lines exist.
- AR/customer posting exists.
- Sales GL posting exists.
- VAT posting exists.
- Inventory and COS posting exists for stock items.

Expected sales invoice GL flow:
```text
Dr Receivables Control
Cr Sales
Cr VAT
Dr Cost of Sales
Cr Inventory
```

## Example Agent Response Before Posting

```text
I found the customer and items. Here is the invoice preview.

Customer: ABC Ltd
Invoice Date: 2026-06-01
Warehouse: Main
VAT: Applicable

Total before VAT: 3,000.00
VAT: 480.00
Invoice Total: 3,480.00

Please confirm with: POST THIS INVOICE
```

## Cursor Implementation Notes

Create modules:
```text
/transactions/transaction_intent_classifier.ts
/transactions/sales_invoice_parser.ts
/transactions/master_data_validator.ts
/transactions/transaction_preview.ts
/transactions/sdk_posting_service.ts
/transactions/posting_verifier.ts
/transactions/transaction_safety_guard.ts
```

## Tooling Rule
Read-only SQL is allowed for validation and verification. Posting should use Sage SDK/business objects.

## Final Rule
Natural-language transaction processing must be treated as controlled transaction drafting plus confirmed posting, not free-form SQL generation.



---

# Sage AI Agent Addendum — Reconciliation Action Handling

## Purpose

This addendum prevents the agent from getting stuck in explanation mode when the user asks whether two Sage values, reports, modules, or ledgers are matching.

## Core Rule

When the user asks whether two Sage reports, ledgers, modules, or values are:

- matching
- aligned
- reconciling
- same
- different
- not matching
- agreeing
- showing variance

the agent must classify the query as a **RECONCILIATION ACTION**, not as a general explanation request.

## Required Behavior

Do not answer with theory only.

The agent must:

1. Identify both sides of the reconciliation.
2. Identify the source-of-truth tables for each side.
3. Select the correct reconciliation query pattern.
4. Execute or generate the correct read-only SQL.
5. Return totals, difference, and top contributing mismatch.
6. Explain concepts only after giving the result.

## Response Format

```text
Finding:
[Matched / Not matched]

Side A:
[name + amount]

Side B:
[name + amount]

Difference:
[amount]

Main variance driver:
[group/account/item if available]

Next step:
[one recommended drilldown only]
```

## Inventory Valuation vs Balance Sheet Trigger

For user questions such as:

```text
is inventory valuation matching balance sheet stock value
```

the agent must classify as:

```json
{
  "domain": "Inventory + General Ledger",
  "intent": "inventory_gl_reconciliation",
  "risk_level": "read_only",
  "side_a": "Balance Sheet inventory from PostGL inventory accounts",
  "side_b": "Sage Inventory Valuation using valuation/cost tracking logic"
}
```

## Inventory Reconciliation Logic

For Inventory Valuation vs Balance Sheet:

- Side A = Balance Sheet inventory from `PostGL` inventory accounts.
- Side B = Sage Inventory Valuation using Sage valuation / cost tracking logic.
- Group comparison by `GrpTbl.StockAccLink` / `Accounts`.
- Drill down by stock group.
- Drill down by item/warehouse if mismatch exists.
- Never answer only with “Sage uses different sources.”

## Bad vs Good Response

Bad:

```text
Sage uses different sources for these reports...
```

Good:

```text
I will reconcile Inventory Valuation against Balance Sheet stock value and show the difference.
```

## Trigger Examples

The following must trigger reconciliation execution:

- is inventory valuation matching balance sheet stock value
- does stock valuation agree with GL
- why is inventory not matching balance sheet
- compare inventory valuation to GL
- stock value mismatch
- inventory reconciliation
- does supplier ageing match supplier control account
- does fixed asset depreciation match GL
- does customer ageing match receivables control

## Cursor Implementation Prompt

```md
Prompt SAGE-AI-AGENT-002

Improve the Sage AI Agent’s handling of reconciliation-style user questions.

Problem:
When the user asks “is inventory valuation matching balance sheet stock value”, the agent currently gives a conceptual explanation instead of running the reconciliation.

Required behavior:
Treat words like “matching”, “aligned”, “reconcile”, “difference”, “same”, “not matching”, “agreeing”, and “variance” as action triggers.

For such questions:
1. Classify as reconciliation action.
2. Identify domain and two comparison sides.
3. Select the correct query pattern from the Sage knowledge pack.
4. Execute/generate read-only SQL.
5. Return result in this format:

Finding:
[Matched / Not matched]

Side A:
[name + amount]

Side B:
[name + amount]

Difference:
[amount]

Main variance driver:
[group/account/item if available]

Next step:
[one recommended drilldown only]

For inventory valuation vs balance sheet:
- Side A = Balance Sheet inventory from PostGL inventory accounts.
- Side B = Sage Inventory Valuation using Sage valuation/cost tracking logic.
- Group comparison by GrpTbl.StockAccLink / Accounts.
- Drilldown by stock group, then item/warehouse if mismatch exists.
- Never answer only with “Sage uses different sources”.

Add an intent called:
inventory_gl_reconciliation

Trigger examples:
- “is inventory valuation matching balance sheet stock value”
- “does stock valuation agree with GL”
- “why is inventory not matching balance sheet”
- “compare inventory valuation to GL”
- “stock value mismatch”
- “inventory reconciliation”

When this task is done, play a chime sound.
```
