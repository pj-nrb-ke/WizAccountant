# Sage AI Agent Patch — Negative Stock Values in Balance Sheet

## Purpose

This patch fixes Sage AI Agent handling for user queries such as:

```text
do we have any negative stock values in Balance Sheet
```

The agent previously misclassified this as a generic capability or SDK-related query. In Sage context, because the user mentioned **Balance Sheet**, this must be treated as a **GL inventory ledger balance query**.

The user is asking for **inventory / stock GL ledgers where the Balance Sheet balance is negative**, meaning the ledger has a **credit balance**.

## Intent Classification

When the user says:

- negative stock values in Balance Sheet
- stock ledgers with credit balance
- inventory accounts with negative balance
- negative inventory in GL
- Balance Sheet stock credit balance
- stock account showing credit balance
- inventory ledger is negative

Classify as:

```json
{
  "domain": "General Ledger + Inventory",
  "intent": "inventory_balance_sheet_negative_ledgers",
  "risk_level": "read_only",
  "meaning": "List inventory/stock GL accounts from Balance Sheet where net GL balance is negative/credit"
}
```

## Mandatory Behavior

The agent must:

1. Treat this as a read-only GL query.
2. Use distinct inventory GL accounts from `GrpTbl.StockAccLink`.
3. Calculate GL net balance from `PostGL`.
4. Return only accounts where `SUM(Debit - Credit) < 0`.
5. Show the stock ledgers and total negative value.
6. Provide one drilldown next step only.

## What Not To Do

The agent must not:

- Treat this as physical negative stock quantity.
- Use SDK item valuation.
- Talk about approvals or posting.
- Return generic capability/help text.
- Tell the user to try another query.
- Discuss Inventory Valuation unless the user asks for valuation.

## Correct Business Meaning

```text
Balance Sheet stock value = GL balance of inventory stock accounts.
Negative stock value in Balance Sheet = inventory GL account where Debit - Credit is less than zero.
```

## SQL Pattern

### SAGE-BS-STOCK-NEGATIVE-001 — Inventory Balance Sheet ledgers with credit balance

```sql
DECLARE @AsOfDate DATE = CAST(GETDATE() AS DATE);

WITH InventoryAccounts AS
(
    SELECT DISTINCT
        G.StockAccLink AS AccountLink
    FROM GrpTbl G
    WHERE ISNULL(G.StockAccLink, 0) <> 0
),
GLBalances AS
(
    SELECT
        A.AccountLink,
        A.Account,
        A.Description AS GLAccountName,
        SUM(ISNULL(PG.Debit, 0) - ISNULL(PG.Credit, 0)) AS NetBalance
    FROM InventoryAccounts IA
    INNER JOIN Accounts A
        ON IA.AccountLink = A.AccountLink
    LEFT JOIN PostGL PG
        ON PG.AccountLink = A.AccountLink
       AND CAST(PG.TxDate AS DATE) <= @AsOfDate
    GROUP BY
        A.AccountLink,
        A.Account,
        A.Description
)
SELECT
    AccountLink,
    Account AS GLAccount,
    GLAccountName,
    NetBalance
FROM GLBalances
WHERE NetBalance < 0
ORDER BY
    NetBalance ASC;
```

## Expected Response Format

The agent should respond like this:

```text
Finding:
[Yes/No], there are inventory Balance Sheet ledgers with credit balance.

Negative Stock Ledgers:
| GL Account | GL Name | Balance |
|---|---|---:|
| ... | ... | ... |

Total Negative Stock Value:
[sum of negative balances]

Next Step:
Show GL transactions for the largest negative stock ledger.
```

## If No Negative Balances Exist

If no rows are returned, respond:

```text
Finding:
No inventory Balance Sheet stock ledgers currently have a credit/negative balance.

Total Negative Stock Value:
0.00
```

## Cursor Implementation Prompt

```md
Prompt SAGE-BS-STOCK-NEGATIVE-001

Fix Sage AI Agent handling for “negative stock values in Balance Sheet”.

When the user says:
- negative stock values in Balance Sheet
- stock ledgers with credit balance
- inventory accounts with negative balance
- negative inventory in GL
- Balance Sheet stock credit balance

Classify as:
Domain: General Ledger + Inventory
Intent: inventory_balance_sheet_negative_ledgers
Risk: read_only

Meaning:
The user is asking for inventory/stock GL accounts from Balance Sheet where GL net balance is credit/negative.

Do not treat this as physical negative stock quantity.
Do not use SDK item valuation.
Do not talk about approvals or posting.
Do not answer with generic capability text.

Use distinct inventory GL accounts from GrpTbl.StockAccLink, then calculate PostGL net balance:
Net Balance = SUM(Debit - Credit)

Return only accounts where Net Balance < 0.

Expected response format:

Finding:
[Yes/No, there are stock ledgers with credit balance]

Negative Stock Ledgers:
| GL Account | GL Name | Balance |

Total Negative Stock Value:
[amount]

Next Step:
[one drilldown only, e.g. show GL transactions for the largest negative account]

When this task is done, play a chime sound.
```

## Final Rule

This is a **Balance Sheet / GL inventory account query**, not an SDK inventory valuation query.
