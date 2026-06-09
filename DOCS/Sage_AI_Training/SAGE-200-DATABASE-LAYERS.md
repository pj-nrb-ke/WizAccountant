# Sage 200 Evolution — database layers (for Insight AI)

Condensed from client handover investigations. Full narrative: `Sage_200_Evolution_Database_Handover.md` (keep in `DOCS/` or client audit folder).

## Rule for the AI assistant

**Match the user’s question to the correct Sage layer.** Do not answer a Balance Sheet question from stock movement tables, or inventory valuation from a simple `PostST` sum.

| User topic | Sage layer | WizAccountant read (pilot) | SQL source (connector future) |
|------------|------------|---------------------------|-------------------------------|
| Customers, unpaid invoices, AR open items | **Accounts receivable** subledger | `customer.openitems`, `customer.unpaid.summary`, `customer.list` | `CustomerTransaction` / PostAR via SDK |
| Suppliers, unpaid AP | **Accounts payable** | `supplier.openitems`, `supplier.list` | `SupplierTransaction` via SDK |
| Stock valuation, items over $X | **Inventory costing** | `inventoryitem.list` (+ `minValuation`) | `StkItem`, `_evInvCostTracking`, `_efnLastCostByDatePerItem` — not raw `PostST` sum |
| Balance sheet, trial balance, GL postings | **General ledger** | `gltransaction.list` (sample rows) | `PostGL`, `Accounts` |
| Dashboard KPIs | **Mixed counts** | `dashboard.summary` | SDK list counts |
| Fixed asset depreciation | **FA subledger** | *Not in chat yet* | `_btblFAGLBatchAssetValues`, `_btblFAAsset` — compare to `PostGL` before judging “wrong month” |
| Manufacturing / BOM | **MFG + stock** | *Not in chat yet* | `PostST`, `_etblManufProcess*`, `PostGL` |

## Principles (from investigations)

1. **PostGL ≠ operational subledger** — Balance Sheet / TB use `PostGL`. Inventory Valuation uses costing views (`_evInvCostTracking`, `_efnLastCostByDatePerItem`). FA depreciation reports use `_btblFAGLBatchAssetValues`, not PostGL alone.

2. **PostST.AccountLink is the stock item**, not the GL account — GL is `PostST.iGLAccountID`.

3. **Stock group drives GL mapping** — `StkItem` → `_etblStockDetails` → `GrpTbl` → `Accounts` (StockAccLink, COSAccLink, etc.).

4. **Service items** (`ServiceItem = 1`) can still post to inventory accounts if mapped to an inventory stock group (DSTK / packaging example in handover).

5. **AR open lines** — Exclude payment/receipt lines when ranking “unpaid invoices per customer”. Prefer invoice/sales-order lines (`OInv`, description).

## Chat routing (code)

- `Insight/ChatIntentMatcher.cs` — keyword → operation  
- `Insight/SageChatDomain.cs` — layer detection + help when no operation matches  
- `Insight/ReadOnlyChatService.cs` — formats answers + layer footnotes  

## Adding new “serious” questions

1. Identify layer (table above).  
2. If no allowlisted op exists, add handler in connector (SDK or controlled SQL), never raw SQL from UI/chat.  
3. Add intent in `ChatIntentMatcher` **before** broad rules (`customer` → full list).  
4. Document in `DOCS/INSIGHT-CHAT-INTENTS.md`.
