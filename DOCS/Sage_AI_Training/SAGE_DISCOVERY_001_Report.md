# SAGE-DISCOVERY-001 — Live Schema Discovery Report

**Database:** DARFORDS2026 (local Sage Evolution)  
**Date:** 2026-05-26  
**Confidence:** High for supplier credit notes and warehouse transfers; medium for customer debit notes (TrCode exists, zero historical usage on this DB).

---

## 1. Customer Debit Note Discovery Report

### Findings

| Question | Answer |
|----------|--------|
| Which TrCode(s)? | **DN** — `TrCodes.idTrCodes = 69`, `Code = 'DN'`, `Description = 'Debit Note'`, **`iModule = 5` (AR)** |
| Stored in InvNum? | **No evidence** — DocType 2 is GRV (353 docs), not customer debit notes. No InvNum DocType 0 on this DB. |
| PostAR-only? | **Yes, when used** — filter `PostAR` debit rows joined to TrCode DN |
| Document number pattern | Reference field on PostAR (when populated) |
| Count reliably? | **Yes** via `COUNT(DISTINCT PostAR.AutoIdx)` + TrCode DN filter |

### Tables / columns

| Table | Columns |
|-------|---------|
| `PostAR` | `AutoIdx`, `TrCodeID`, `AccountLink`, `Reference`, `Description`, `Debit`, `TxDate`, `InvNumKey` |
| `TrCodes` | `idTrCodes`, `Code`, `Description`, `iModule` |
| `Client` | `DCLink`, `Account`, `Name` |

### Live data note

**Zero DN postings** on DARFORDS2026. TrCode is configured but unused. Handler returns zero correctly; will work when DN documents are posted.

### Implementation

- `salesdebitnote.count|list|summary|top` → PostAR + TrCode **DN** (`SageTrCodeSqlHelper.PostArDebitNoteFilter`)

---

## 2. Supplier Credit Note Discovery Report

### Findings

| Question | Answer |
|----------|--------|
| Is DocType 3 sufficient? | **Yes** — RTS on InvNum; 923 total, **357 in 2025** |
| AP credit notes separately? | PostAP TrCode **CN** (iModule 6): **0 rows** on this DB |
| Which TrCodes? | Inventory stock RTS TrCode (id 39) is stock movement; **supplier credit header = InvNum DocType 3** |
| Identify reliably? | **Yes** — `InvNum.DocType = 3` + DocState exclusion + `Vendor` join on `AccountID` |

### Tables / columns

| Table | Columns |
|-------|---------|
| `InvNum` | `AutoIndex`, `DocType`, `InvNumber`, `InvDate`, `InvTotIncl`, `DocState`, `AccountID` |
| `Vendor` | `DCLink`, `Account`, `Name` |

### Sample 2025 Q1

- Count (distinct AutoIndex): **157**
- Total value: **67,963,215.17**

### Implementation

- `suppliercreditnote.count|list|summary|top` → InvNum **DocType 3 (RTS)** (`InvNumSqlHelper.SupplierRtsDocTypeFilter`)

---

## 3. Warehouse Transfer Discovery Report

### Findings

| Question | Answer |
|----------|--------|
| Which TrCodes? | **WHT** (id 32) — Warehouse Transfer; **WHTC** (id 40) — Warehouse Transfer Supplier Costs |
| From / to warehouses? | **Yes** — paired lines on same `Reference` + `TxDate`: outbound `TransQtyOut` (from), inbound `TransQtyIn` (to) on `_bvSTTransactionsFull` |
| Without text matching? | **Yes** — `TrCode IN ('WHT','WHTC')` |
| Authoritative source | **`_bvSTTransactionsFull`** (includes `WarehouseCode`, `TransQtyIn`, `TransQtyOut`, `AccountLink` → `StkItem`) |

### 2025 activity

- WHT/WHTC lines: **3,858** (outbound legs ~1,929 transfer events)

### Implementation

- Replaced description `%transfer%` matching with **TrCode WHT/WHTC**
- Added `warehouse.transfer.detail|top|by.item|by.warehouse`

---

## 4. Handlers implemented

| Operation family | Source |
|------------------|--------|
| `salesdebitnote.*` | PostAR + TrCode DN |
| `suppliercreditnote.*` | InvNum DocType 3 |
| `warehouse.transfer.*` | `_bvSTTransactionsFull` + WHT/WHTC |

---

## 5. Remaining unknowns

1. **Customer debit notes on this DB** — TrCode DN configured but never used; if your site posts debit notes via another TrCode, re-run discovery on PostAR debit frequency by TrCode description.
2. **DocState semantics on this build** — Posted documents often use DocState **4** (not in original 0–7 table); analytics exclude 2,5,6,7 only.
3. **RTS duplicate AutoIndex** — Same InvNumber can appear in DocState 1 and 4; counts use **DISTINCT AutoIndex**.
4. **Purchase DocType 5 + DocFlag** — Supplier invoice flag filter not yet applied (P1 from prior clarifications).

---

## 6. Discovery SQL reference

See `InvNumDocumentSchemaHintHandler` operation `insight.sql.invnum-documents-hint` for repeatable probes.
