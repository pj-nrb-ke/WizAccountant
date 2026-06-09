# SAGE-DOCS-001 Clarification Response V2

This document provides the recommended business and implementation responses to the schema clarification questions raised in SAGE-DOCS-001.

## Executive Summary

### Immediate P0 Fixes
1. Replace `DocState <> 3` logic.
2. Exclude cancelled documents using `DocState = 7`.
3. Remove `DocType = 1` from purchase filters.
4. Keep customer credit notes on `DocType = 1`.
5. Do not assume customer debit notes are `DocType = 2`.

### Customer Debit Notes
- NOT confirmed as InvNum DocType 2.
- Must be discovered from PostAR and TrCodes.
- Implement only after source verification.

### Supplier Credit Notes
- Likely RTS (DocType 3) and/or PostAP credit transactions.
- Must be confirmed from live database.

### Sales Invoices
Recommended logic:

DocType = 0
OR
DocType = 4 + Invoice Document Flag

after flag confirmation.

### Purchase Invoices
Remove DocType 1 completely.
Use confirmed purchase invoice document type after schema discovery.

### Warehouse Transfers
Current text matching is insufficient.
Move to:
- inventory TrCodes
- warehouse movement sources
- _bvSTTransactionsFull / PostST verification

### Collections
Collections must route to:
customer.collections.*

and never:
Customer.List

Use PostAR credit transactions with customer joins.

## P0 Decisions

### Cancelled Documents

Exclude:

- DocState 7

Recommended exclusion for analytics:

- 2 Quote
- 5 Template
- 6 Contract Template
- 7 Cancelled

### Sales Credit Notes

Keep:

DocType = 1

POS Credit Notes (DocType 7):
- Separate handler recommended

### Purchase Filter

Remove:

DocType = 1

from all purchase logic.

## Discovery Required Before P2

### Customer Debit Notes
Confirm:
- TrCode IDs
- TrCode Codes
- Reference patterns
- PostAR linkage

### Supplier Credit Notes
Confirm:
- RTS usage
- AP transaction types
- PostAP credit behavior

### Warehouse Transfers
Confirm:
- Transfer TrCodes
- From warehouse field
- To warehouse field
- Source tables

## Final Instruction To Cursor

1. Apply P0 fixes immediately.
2. Run schema discovery.
3. Confirm document flag column.
4. Correct sales filters.
5. Correct purchase filters.
6. Implement supplier credit note handlers.
7. Implement customer debit note handlers.
8. Improve warehouse transfer handlers.
9. Add regression tests.

Do not guess DocTypes.
Do not confuse balances with documents.
Do not implement debit notes using GRV assumptions.
