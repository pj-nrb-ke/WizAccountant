# Sage AI Agent Patch — Count Queries vs Transaction Listing

## Problem

User Query:

```text
how many sales invoices in 2026 have discounts in them ?
I need the Total Count of Invoices
```

Incorrect AI Agent Response:

```text
Query run: CustomerTransaction.List (Outstanding <> 0)

Returned 9373 rows
Showing 500 of 9373
```

This response is completely misclassified.

The user requested:

```text
COUNT of discounted sales invoices
```

But the Agent returned:

```text
Open AR transaction listing
```

---

## Root Cause

The AI Agent incorrectly matched:

```text
sales invoices
```

to:

```text
CustomerTransaction.List
```

and incorrectly matched:

```text
how many
```

to:

```text
Return rows
```

instead of:

```text
COUNT aggregation
```

The Agent also ignored:

```text
discounts in them
```

which is the main analytical condition.

---

## Required Semantic Interpretation

The query means:

```text
Count sales invoices in 2026 where discount > 0
```

Expected output:

```text
Total Sales Invoices with Discounts in 2026: 214
```

NOT:

```text
9373 records showing 500
```

---

## Mandatory AI Agent Rule

When user says:

```text
how many
count
total count
number of
```

The AI Agent must switch into:

```text
AGGREGATION MODE
```

and MUST NOT:

- Return transaction grids
- Return listing tables
- Return 500 rows
- Return generic CustomerTransaction.List results

---

## Mandatory AI Agent Rule — Discount Detection

When user says:

```text
discount
discounted invoices
highest discounts
discount value
discount percentage
```

The Agent must:

```text
Search invoice discount fields
```

NOT:

```text
Search outstanding AR transactions
```

---

## Correct SQL Logic

Correct semantic logic:

```sql
COUNT Sales Invoices
WHERE:
InvoiceDate in 2026
AND Discount > 0
```

Example:

```sql
SELECT COUNT(*) AS TotalInvoices
FROM InvNum
WHERE DocType = 4
AND InvDate >= '2026-01-01'
AND InvDate < '2027-01-01'
AND (
    ISNULL(DiscValue,0) > 0
    OR ISNULL(DiscPercentage,0) > 0
)
```

Actual field names may vary depending on Sage schema.

---

## Correct Expected Response

```text
Total Sales Invoices with Discounts in 2026: 214
```

Optional enhancement:

```text
Average Discount Value: 12,420.55
Highest Discount Invoice: INV000452
```

---

## AI Agent Classification Rules

### If user says:

```text
how many
count
number of
total invoices
```

Switch to:

```text
Aggregation Query Mode
```

Expected response style:

```text
Single numeric/statistical answer
```

NOT:

```text
Transaction grid
```

---

## Output Formatting Rules

For COUNT queries:

DO:

```text
Total Count: 214
```

DO NOT:

```text
Showing 500 of 9373 rows
```

---

## Correct Processing Workflow

```text
User Query
→ Detect Aggregation Intent
→ Detect Business Domain
→ Detect Filters/Conditions
→ Build COUNT SQL
→ Return Single Aggregated Result
```

---

## Final Principle

The AI Agent must understand:

```text
how many
```

means:

```text
Aggregate
```

NOT:

```text
Dump transactions
```

The Agent must behave like:

```text
Business analytics assistant
```

Not:

```text
Generic transaction browser
```
