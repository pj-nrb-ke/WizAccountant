# SAGE-DOCS-001 — Schema Clarifications & Response Sheet

**Purpose:** Consolidate Sage Evolution document flags (DocType, DocState, Document Flag, iModule) against what WizAccountant handlers assume today. Review this file, fill in the **Your response** sections (or add inline comments), and return it so we can implement handlers without wrong DocType routing.

**Related work:** SAGE-DOCS-001 (customer debit notes, supplier credit notes, warehouse transfers), SAGE-PATCH-010 (customer collections), sales credit note count.

**Status:** P0 fixes applied per [SAGE_DOCS_001_Clarification_Response_V2.md](./SAGE_DOCS_001_Clarification_Response_V2.md). P2 handlers (debit notes, supplier credit notes, warehouse TrCodes) blocked on `insight.sql.invnum-documents-hint` results.

---

## How to respond

1. Read each **Question** block.
2. Under **Your response**, write answers, paste query results, or mark **Confirm** / **Reject** / **Unsure**.
3. For SQL discovery sections, paste result tables (or screenshots summarized as markdown tables).
4. If a column name differs on your build, note the actual name.
5. Save this file and tell the agent to read `DOCS/SAGE_DOCS_001_Schema_Clarifications.md` again.

---

## 1. Authoritative flags (from your input)

These are recorded as the source of truth unless you correct them below.

### 1.1 InvNum DocType

| Value | Document type |
|------:|---------------|
| 0 | Invoice |
| 1 | Credit Note |
| 2 | GRV (Goods Received Voucher) |
| 3 | RTS (Return to Supplier) |
| 4 | Sales Order |
| 5 | Purchase Order |
| 6 | POS Inv |
| 7 | POS Crn |
| 8 | Job Costing Invoice |

**Your response:** Confirm table as-is, or list corrections:

```
(Your corrections here)
```

---

### 1.2 InvNum DocState

| Value | Meaning |
|------:|---------|
| 0 | Unknown |
| 1 | Unprocessed |
| 2 | Quote |
| 3 | Partial processed |
| 4 | Archived |
| 5 | Template |
| 6 | Contract template (removed on build 5.00 — templates use state 5) |
| 7 | Cancelled orders |

**Your response:** Confirm table as-is, or list corrections:

```
(Your corrections here)
```

---

### 1.3 Document Flag (depends on DocType)

**For GRV (DocType 2) or PO (DocType 5):**

| Flag | Meaning |
|------:|---------|
| 0 | Not separating GRV and supplier invoice |
| 1 | Goods Received Voucher |
| 2 | Supplier Invoice |

**For Invoice (DocType 0) or Sales Order (DocType 4):**

| Flag | Meaning |
|------:|---------|
| 0 | Not separating invoice and issue stock |
| 1 | Issue stock |
| 2 | Invoice |

**Your response:**

- **Q1.3a** — What is the **exact SQL column name** on `InvNum` for this flag? (e.g. `DocFlag`, `InvDocState`, other)

```
Column name:
```

- **Q1.3b** — Confirm flag value meanings above for your site (yes/no + notes):

```
```

---

### 1.4 CashBook / PostAR / PostAP / TrCode iModule

| iModule | Module |
|--------:|--------|
| 0 | General Ledger |
| 1 | Accounts Receivable |
| 2 | Accounts Payable |
| 11 | Inventory / Bill of Materials |
| 15 | Point of Sale |
| 44 | Retail Point of Sale |

**TrCode table** uses the same iModule values to classify transaction types per module.

**Your response:** Confirm table as-is. Note if your TrCode table name differs (`TrCodes`, `TrCode`, `_btblTrCodes`, etc.):

```
TrCode table name:
Other module value notes:
```

---

## 2. What the codebase assumes today (needs reconciliation)

### 2.1 `InvNumSqlHelper.cs` filters

| Constant | Current SQL | Issue per your flags |
|----------|-------------|----------------------|
| `SalesDocTypeFilter` | `DocType = 4 OR DocType IN (0, 4)` | DocType **4 = Sales Order**, not necessarily posted invoice; may need Document Flag = 2 |
| `PurchaseDocTypeFilter` | `DocType = 5 OR DocType IN (1, 5)` | DocType **1 = Credit Note (AR)** — must not be in purchase filter |
| `SalesCreditNoteDocTypeFilter` | `DocType = 1` | **Aligned** with Credit Note |

### 2.2 DocState “cancelled” filter (bug)

These files use `ISNULL(H.DocState, 0) <> 3` meaning “exclude cancelled”:

- `SalesCreditNoteCountHandler.cs`
- `ProductMonthlyOrdersAnalysisHandler.cs`
- `InvoiceLineSqlHelper.cs`

Per your flags, **DocState 3 = Partial processed** and **DocState 7 = Cancelled**. The filter should likely be `<> 7`, not `<> 3`.

**Your response — Q2.2:**

For **count/total queries** (credit notes, sales invoices, purchase invoices), which DocState values should be **included**?

Check all that apply, or list explicitly:

- [ ] 0 Unknown
- [ ] 1 Unprocessed
- [ ] 2 Quote
- [ ] 3 Partial processed
- [ ] 4 Archived
- [ ] 5 Template
- [ ] 6 Contract template (legacy)
- [ ] 7 Cancelled — always exclude?

```
Notes (e.g. "only fully processed/posted docs" rule):
```

---

## 3. Document families — open questions

### 3.1 Customer sales credit notes (AR)

**Planned / partial implementation:** `salescreditnote.count` — InvNum `DocType = 1`.

**Your response — Q3.1:**

- Confirm **DocType 1** is the only InvNum type for **customer** credit notes on your site? (POS Crn DocType 7 separate?)

```
```

- Should POS credit notes (DocType 7) be included in “sales credit notes” chat queries?

```
Yes / No / Separate handler:
```

- Value field: is **`InvTotIncl`** correct for total credit note value?

```
```

---

### 3.2 Customer debit notes (AR)

**Not in your DocType list.** We previously assumed DocType 2 — that is **GRV (AP)**, not customer debit notes.

**Your response — Q3.2:**

How are **customer debit notes** represented in your Sage database?

- [ ] InvNum DocType 0 (Invoice) with negative amounts
- [ ] InvNum DocType 0 with a specific Document Flag
- [ ] InvNum DocType 4 (Sales Order) variant
- [ ] PostAR only (no InvNum header) — specify TrCode(s):
- [ ] DocType 8 Job Costing Invoice
- [ ] Other:

```
TrCode ID(s) or Code(s) if PostAR:
Sample InvNumber / Reference pattern:
Should chat phrase "debit note" map here? (yes/no)
```

**Discovery query** (run on company DB if helpful):

```sql
-- Recent AR debit-side customer documents
SELECT TOP 30
    P.AutoIdx, P.TrCodeID, T.Code AS TrCode, T.Description AS TrCodeDesc,
    P.Reference, P.Description, P.Debit, P.Credit, P.TxDate, P.InvNumKey
FROM PostAR P
LEFT JOIN TrCodes T ON T.idTrCodes = P.TrCodeID   -- adjust join if table/column differs
WHERE ISNULL(P.Debit, 0) > 0
ORDER BY P.TxDate DESC;
```

Paste results or summary:

```
```

---

### 3.3 Supplier credit notes (AP)

Candidates per your flags:

- InvNum **DocType 3 (RTS)**
- InvNum **DocType 2 (GRV)** with Document Flag
- **PostAP** with AP module TrCodes
- DocType 1 might be AR-only (customer credit note)

**Your response — Q3.3:**

Primary source for **supplier credit notes** on your site:

- [ ] InvNum DocType 3 (RTS)
- [ ] InvNum DocType 2 + Document Flag = ?
- [ ] PostAP only
- [ ] Combination (describe):

```
TrCode ID(s) or Code(s):
Value column (InvTotIncl on InvNum / Debit-Credit on PostAP):
Exclude DocState 7 only, or stricter rule?
```

**Discovery queries:**

```sql
-- RTS headers
SELECT TOP 20 AutoIndex, DocType, InvNumber, InvDate, AccountID, InvTotIncl, DocState
FROM InvNum
WHERE DocType = 3
ORDER BY InvDate DESC;

-- DocType distribution (sanity check)
SELECT DocType, DocState, COUNT(*) AS Cnt
FROM InvNum
GROUP BY DocType, DocState
ORDER BY DocType, DocState;
```

Paste results or summary:

```
```

---

### 3.4 Sales invoices (for existing handlers)

Many handlers use `SalesDocTypeFilter` (DocType 0 and 4).

**Your response — Q3.4:**

For **posted tax invoices / sales invoices** used in VAT, discount, and customer sales handlers:

- [ ] DocType 0 only
- [ ] DocType 4 + Document Flag = 2 (Invoice)
- [ ] DocType 0 OR (DocType 4 AND Flag = 2)
- [ ] DocType 0 OR DocType 4 regardless of flag
- [ ] Other:

```
```

- Should **Sales Order** (DocType 4) without flag 2 ever count as “invoice issued”?

```
```

---

### 3.5 Purchase invoices / supplier invoices (AP)

Current filter includes DocType 1 (wrong) and DocType 5.

**Your response — Q3.5:**

For **supplier invoices** (not GRV, not PO unprocessed):

- [ ] DocType 5 + Document Flag = 2
- [ ] DocType 5 regardless of flag
- [ ] DocType 2 + Document Flag = 2 (Supplier Invoice on GRV module)
- [ ] PostAP only
- [ ] Other:

```
```

- For **GRV** (goods received, not yet invoiced): DocType 2 + Flag 1 — should chat queries about “purchase invoices” **exclude** these?

```
Yes / No:
```

---

### 3.6 POS documents (DocType 6, 7)

**Your response — Q3.6:**

- Include POS Inv (6) and POS Crn (7) in standard “sales invoice” / “credit note” chat totals?

```
Sales invoices: Yes / No / Separate
Credit notes: Yes / No / Separate
```

---

### 3.7 Job Costing Invoice (DocType 8)

**Your response — Q3.7:**

- Include DocType 8 in “sales invoices” for general business chat?

```
Yes / No / Only when user mentions job costing:
```

---

## 4. Warehouse transfers

**Current handler:** `warehouse.transfer.summary` — filters `_bvSTTransactionsFull` by `%transfer%` / `%trf%` in Description, Reference, or TrCode text.

**Your response — Q4.1:**

- Correct stock view: `_bvSTTransactionsFull`, `PostST`, or other?

```
View/table name:
```

- Should transfers be identified by **TrCode iModule = 11** + specific TrCode IDs instead of text matching?

```
TrCode ID(s) or Code(s) for warehouse transfers:
```

- Are **from-warehouse / to-warehouse** columns available on that view? Column names:

```
From warehouse column:
To warehouse column:
```

**Discovery query:**

```sql
SELECT TOP 30
    T.TxDate, T.TrCode, T.Reference, T.Description,
    T.WarehouseID, T.TransQtyIn, T.TransQtyOut
FROM _bvSTTransactionsFull T
WHERE CAST(T.TxDate AS DATE) >= DATEADD(day, -90, GETDATE())
  AND (
        LOWER(ISNULL(T.Description, '')) LIKE '%transfer%'
        OR LOWER(ISNULL(T.Reference, '')) LIKE '%transfer%'
      )
ORDER BY T.TxDate DESC;
```

Optional — TrCodes for inventory module:

```sql
SELECT iModule, Id, Code, Description
FROM TrCodes    -- adjust table name
WHERE iModule = 11
ORDER BY Code;
```

Paste results or summary:

```
```

---

## 5. Customer collections (SAGE-PATCH-010)

Collections handlers use **PostAR** with **`Credit > 0`** joined to **Client** (not `Customer`).

**Your response — Q5.1:**

- Confirm **`Client`** is the customer master table name on your DB?

```
Yes / No — actual table name:
```

- Confirm receipt/collection TrCodes or rule: “any PostAR Credit in period”?

```
TrCode filter (if any):
Exclude certain TrCodes:
```

---

## 6. Table and column naming on your build

Please confirm or correct common names used in discovery SQL:

| Concept | Name we use | Your actual name |
|---------|-------------|------------------|
| Customer master | `Client` | |
| Supplier master | `Vendor` / `Supplier` | |
| Invoice header | `InvNum` | |
| Invoice lines | `_btblInvoiceLines` | |
| AR postings | `PostAR` | |
| AP postings | `PostAP` | |
| TrCode master | `TrCodes` | |
| TrCode FK on postings | `TrCodeID` | |
| Warehouse master | `WhseMst` | |
| Stock transactions view | `_bvSTTransactionsFull` | |

**Your response:**

```
(Fill in corrections)
```

---

## 7. Sage build / version

**Your response — Q7.1:**

- Sage Evolution version / build (e.g. 5.00):

```
```

- Any custom modules or document types not in section 1?

```
```

---

## 8. Proposed handler map (pending your confirmation)

| Chat intent (examples) | Proposed operation family | Proposed data source | Pending your Q# |
|------------------------|---------------------------|----------------------|-----------------|
| “Total credit notes in Q1” | `salescreditnote.*` | InvNum DocType 1 | Q3.1, Q2.2 |
| “Debit notes to customers” | `salesdebitnote.*` | **TBD** — PostAR and/or InvNum | Q3.2 |
| “Supplier credit notes” | `suppliercreditnote.*` | InvNum DocType 3 RTS and/or PostAP | Q3.3 |
| “Purchase invoices this month” | existing `purchase.invoice.*` | InvNum DocType 5 + Flag 2? | Q3.5, Q2.2 |
| “Sales invoices / VAT output” | existing sales/VAT handlers | InvNum DocType 0 / 4 + Flag? | Q3.4, Q2.2 |
| “Warehouse transfers” | `warehouse.transfer.*` | PostST / stock view + TrCode 11 | Q4.1 |
| “Collections from customers” | `customer.collections.*` | PostAR Credit, Client join | Q5.1 |

**Your response:** Approve map, or note changes:

```
```

---

## 9. Discovery queries — full checklist

Run any subset on a **copy or read-only** company database. Paste results into the matching section above.

### 9.1 InvNum DocType × DocState matrix

```sql
SELECT DocType, DocState, COUNT(*) AS Cnt
FROM InvNum
GROUP BY DocType, DocState
ORDER BY DocType, DocState;
```

### 9.2 Document Flag column (after name confirmed)

```sql
-- Replace DocFlag with actual column name
SELECT DocType, DocFlag, COUNT(*) AS Cnt
FROM InvNum
WHERE DocType IN (0, 2, 4, 5)
GROUP BY DocType, DocFlag
ORDER BY DocType, DocFlag;
```

### 9.3 Sample credit notes (DocType 1)

```sql
SELECT TOP 10 AutoIndex, InvNumber, InvDate, InvTotIncl, DocState, AccountID
FROM InvNum
WHERE DocType = 1
ORDER BY InvDate DESC;
```

### 9.4 Sample RTS (DocType 3)

```sql
SELECT TOP 10 AutoIndex, InvNumber, InvDate, InvTotIncl, DocState, AccountID
FROM InvNum
WHERE DocType = 3
ORDER BY InvDate DESC;
```

### 9.5 PostAP supplier credits (if applicable)

```sql
SELECT TOP 30
    P.AutoIdx, P.TrCodeID, T.Code, P.Reference, P.Debit, P.Credit, P.TxDate
FROM PostAP P
LEFT JOIN TrCodes T ON T.idTrCodes = P.TrCodeID
WHERE ISNULL(P.Credit, 0) > 0
ORDER BY P.TxDate DESC;
```

---

## 10. Code changes we will make after your response

| Priority | Change | Blocked by |
|----------|--------|------------|
| P0 | Replace `DocState <> 3` with correct cancelled/partial rules | Q2.2 |
| P0 | Remove DocType 1 from `PurchaseDocTypeFilter` | Q3.5 confirm |
| P1 | Add Document Flag to sales/purchase/credit SQL | Q1.3a, Q3.4, Q3.5 |
| P1 | Fix `SalesDocTypeFilter` for SO vs invoice | Q3.4 |
| P2 | Implement `salesdebitnote.*` | Q3.2 |
| P2 | Implement `suppliercreditnote.*` | Q3.3 |
| P2 | Improve `warehouse.transfer.*` (TrCode-based) | Q4.1 |
| P2 | Optional: extend `salescreditnote.list/summary/top` | Q3.1 |

---

## 11. Quick yes/no — minimum needed to start P0 fixes

If you are short on time, answering only these unblocks immediate bug fixes:

1. **Cancelled DocState = 7** — exclude only 7, or also exclude quotes/templates (2, 5)?

```
```

2. **Purchase filter** — remove DocType 1 from purchase queries? (recommended: **yes**)

```
```

3. **Sales credit notes** — stay on DocType 1 only?

```
```

---

## 12. Change log

| Date | Author | Notes |
|------|--------|-------|
| 2026-05-26 | WizAccountant agent | Initial clarification sheet from user DocType/DocState/Flag input |

---

*When finished, save this file and reply in chat: “Review complete — see SAGE_DOCS_001_Schema_Clarifications.md”.*
