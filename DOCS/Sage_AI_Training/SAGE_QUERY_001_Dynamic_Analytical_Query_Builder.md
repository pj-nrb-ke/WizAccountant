# SAGE-QUERY-001 — Dynamic Analytical Query Builder

## Problem

Real user query:

```text
how much CPO (Crude Palm Oil) was bought in Q1, Q2, Q3 & Q4 of 2025. I need total per quarter. Code = DRCPO01 and DRCPO02
```

The manually written SQL worked, but the Agent replied:

```text
Status: No dedicated SQL handler matched this exact wording yet.
```

This is an architecture gap: the system could classify aggregation intent but could not route common analytical queries unless a dedicated handler existed for exact wording.

## Solution

**Controlled dynamic analytical query builder** — not free-form SQL.

When no dedicated static handler matches, but the query is read-only, analytical, and fits a safe template, route to an allowlisted connector operation.

## Implementation

| Component | Location | Role |
|-----------|----------|------|
| `DynamicAnalyticalQueryBuilder` | `src/WizAccountant.Api/Insight/DynamicAnalyticalQueryBuilder.cs` | Pattern detection + parameter extraction |
| `purchase.item.period.summary` | `PurchaseProductQuarterlyHandler.cs` | Parameterized SQL template (quarter or month) |
| Legacy alias | `purchase.product.quarterly` | Backward-compatible connector operation name |
| Routing | `ChatRoutePlanner` | Dynamic builder runs before product monthly analysis and mega-digest |
| Fallback guard | `MegaDigestFallbackMatcher` | Suppresses "no handler matched" when `CanAnswer()` is true |
| Retry | `ReadOnlyChatService` | Second-chance dynamic routing when primary route is null |

## Phase 1 patterns

1. **Item purchase by period** → `purchase.item.period.summary` (implemented)
2. **Item sales by period** → `product.monthly.orders.analysis` when compatible (delegated)
3. Customer collections by period — future
4. Supplier purchases by period — future

## Canonical operation

```text
purchase.item.period.summary
```

Supports:

- Item code filters (regex + explicit codes)
- Item name filters (CPO / Crude Palm Oil → Palm Oil search)
- Year and segmented quarter periods
- `groupBy=quarter|month`
- Quantity and value totals

Query serial: `SAGE-PURCHASE-ITEM-PERIOD-SUMMARY-001`

## Safety rules

- Allowlisted connector operations only (no arbitrary SQL from chat)
- SELECT-only parameterized templates in connector
- InvNum purchase document filter: DocType IN (2,5)
- DocState analytics exclusion per `InvNumSqlHelper`
- Output validated via handler JSON contract (period, quantity, value, items)

## Must not return

```text
No dedicated SQL handler matched this exact wording.
```

when a safe analytical template fits (purchase item by period).

## Tests

`tests/WizAccountant.Insight.Intents.Tests/DynamicAnalyticalQueryBuilderTests.cs`

Positive routing for CPO/quarter/month variants; mega-digest fallback suppressed; misroute guards.

## Restart requirement

After deploy, restart **WizAccountant.Api** and **WizConnector.Service** so new routing and handler registration load.
