# SAGE-PATCH-011 — Suppliers Not Paid Till Date

## Problem

Queries like *"how many suppliers are not paid till date"* or *"which suppliers are not paid till date"* were misread as *"suppliers paid after due date"* or routed without a connector handler, causing:

```text
The connector on this PC does not support this read yet.
```

## Correct meaning

**As-of today:** count or list suppliers with **outstanding AP balance > 0** from open `SupplierTransaction` lines.

Not payment-delay history.

## Operations

| Operation | Use when |
|-----------|----------|
| `supplier.unpaid.count` | how many / count / aggregation |
| `supplier.unpaid.list` | which / list / show suppliers |
| `supplier.unpaid.top` | top / highest / most outstanding |
| `supplier.unpaid.summary` | legacy alias (count or list) |

Query serials: `SAGE-AP-SUPPLIER-UNPAID-COUNT-001`, `LIST-001`, `TOP-001`

## Data logic

- Source: `SupplierTransaction.List("Outstanding <> 0")`
- Per supplier: sum open invoice line outstanding (excludes payment lines)
- Sign: uses `ApSupplierRankingHelper.ResolveOutstanding` (same as other AP handlers)

## Output contract (`supplier.unpaid.count`)

Required JSON fields:

- `totalUnpaidSuppliers`
- `totalOutstandingPayable`
- `asOfDate`

## Restart

Rebuild and restart **WizConnector.Service** and **WizAccountant.Api** after deploy.
