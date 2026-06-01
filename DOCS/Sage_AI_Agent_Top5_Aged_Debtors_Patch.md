# Sage AI Agent Patch — Top 5 Oldest Aged Debtors

## Problem

User Query:

```text
list me top 5 customers with oldest aged debit balances
```

Incorrect Agent Response:

- Returned 500 customers instead of top 5.
- Ignored ranking intent.
- Ignored aging intent.
- Returned generic balance list.

---

## Correct Semantic Interpretation

The phrase:

```text
top 5 customers with oldest aged debit balances
```

Means:

```text
Return ONLY 5 customers
Ranked by oldest outstanding debit invoices
Typically using overdue aging buckets or oldest invoice date
Exclude zero balances and credit balances
```

---

## Required Agent Behavior

The AI Agent must classify this as:

```text
AR Aging Analysis Query
```

Not:

```text
Generic Customer Listing
```

The agent must:

1. Use AR aging/open transaction data
2. Filter only debit/outstanding customers
3. Rank by oldest overdue age
4. Return ONLY requested quantity

---

## Correct SQL Strategy

Preferred logic:

```sql
TOP 5
ORDER BY OldestInvoiceDate ASC
```

or

```sql
TOP 5
ORDER BY DaysOutstanding DESC
```

Only include:

```sql
OutstandingBalance > 0
```

Exclude:

```sql
Zero balances
Credit balances
Cash customers unless specifically requested
```

---

## Example Correct Output

```text
Top 5 Customers with Oldest Aged Debit Balances

1. ABC Traders — Balance: 1,245,000.00 — Oldest Invoice: 412 days
2. XYZ Supplies — Balance: 845,200.00 — Oldest Invoice: 389 days
3. Delta Agencies — Balance: 602,115.50 — Oldest Invoice: 355 days
4. Prime Wholesalers — Balance: 455,000.00 — Oldest Invoice: 341 days
5. Kibo Distributors — Balance: 390,880.00 — Oldest Invoice: 330 days
```

---

## Agent Rule Patch

When user says:

```text
top
oldest
aged
debit balances
overdue
aging
```

The agent must:

```text
Switch to AR Aging semantic mode
Use TOP/LIMIT
Use aging ranking
Avoid dumping full customer master
```

---

## Important Constraint

Never return:

```text
showing 500 of 518
```

When the user explicitly requested:

```text
top 5
```

The result count must obey the user intent strictly.

---

## Recommended Agent Execution Flow

```text
User Query
→ Intent Detection
→ AR Aging Analysis
→ Determine Requested Count
→ Execute Ranked Query
→ Return ONLY Requested Rows
```

---

## Final Principle

The AI Agent must behave like:

```text
Finance-aware analytical assistant
```

Not:

```text
Customer master table dump utility
```
