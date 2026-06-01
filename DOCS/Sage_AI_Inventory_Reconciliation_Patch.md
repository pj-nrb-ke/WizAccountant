# Sage AI Agent Patch — Inventory Valuation Reconciliation Must Use Sage SQL Logic

## 1. Purpose

This patch fixes a specific failure in the Sage AI Agent.

User query:

```text
is inventory valuation matching balance sheet stock value
```

The agent incorrectly compared:

```text
Balance Sheet inventory from PostGL
vs
summed SDK item valuations
```

This is wrong.

The correct comparison is:

```text
Balance Sheet inventory from PostGL inventory accounts
vs
Sage Inventory Valuation by Date using Sage valuation SQL logic
```

The agent must never use a generic SDK item valuation sum for this reconciliation.

## 2. Critical Failure Observed

The agent produced:

```text
Balance Sheet inventory: 83,796,752.11
Inventory valuation SDK sum: 4,079,383.72
Difference: -79,717,368.39
```

This result is invalid because:

1. SDK item valuation is not the Sage Inventory Valuation by Date report.
2. GL balance was duplicated by stock group where multiple stock groups map to the same stock GL account.
3. The agent returned theory and “next step” instead of a final reconciliation result.
4. The agent did not provide the true grand totals required by the user.

## 3. Mandatory Rule

For inventory valuation reconciliation, the agent must use this source-of-truth rule:

```text
Balance Sheet Stock Value = PostGL balance of distinct inventory GL accounts.
Inventory Valuation = Sage Inventory Valuation by Date SQL logic using cost tracking / valuation function.
```

The agent must not use:

```text
SDK item valuation sum
```

for this reconciliation.

## 4. Correct Intent Classification

When the user asks any of the following:

```text
is inventory valuation matching balance sheet stock value
does stock valuation match balance sheet
compare inventory valuation to GL
is stock value aligned with GL
inventory valuation mismatch
stock valuation reconciliation
```

Classify as:

```json
{
  "domain": "Inventory + General Ledger",
  "intent": "inventory_valuation_vs_balance_sheet_reconciliation",
  "risk_level": "read_only",
  "side_a": "Balance Sheet inventory from PostGL distinct inventory GL accounts",
  "side_b": "Sage Inventory Valuation by Date using Sage SQL valuation logic",
  "forbidden_sources": ["SDK item valuation sum"]
}
```

## 5. Required Output

The agent must answer with this structure:

```text
Finding:
Inventory valuation is matching / not matching Balance Sheet stock value.

Balance Sheet Stock Value:
[amount]

Inventory Valuation:
[amount]

Difference:
[amount]

Match:
Yes/No based on tolerance

Main Variance:
[stock group / GL account / item if mismatch exists]

Next Step:
[one drilldown only, if mismatch exists]
```

If the difference is only rounding, say:

```text
The reports are aligned. Difference is only rounding.
```

## 6. Correct GL Side Logic

### 6.1 Do not duplicate GL balances by stock group

A single GL inventory account can be mapped to multiple stock groups.

Example:

```text
NEWWIP
NEWWIP01
NEWWIP02
NEWWIP03
WIP
```

may all map to the same WIP GL account.

If the agent groups GL by stock group and repeats the same GL balance for each group, it will overstate the Balance Sheet side.

### 6.2 Correct GL approach

For grand total Balance Sheet inventory:

```text
Use distinct stock inventory GL accounts first.
Then sum PostGL once per GL AccountLink.
```

Do not sum repeated balances per stock group.

### 6.3 Correct inventory GL account list

Inventory GL accounts should come from:

```text
SELECT DISTINCT GrpTbl.StockAccLink
FROM GrpTbl
WHERE StockAccLink IS NOT NULL AND StockAccLink <> 0
```

Then calculate PostGL balance for those accounts.

## 7. Correct Valuation Side Logic

Inventory Valuation by Date must use Sage valuation logic.

The known Sage-style valuation approach uses:

- `_evInvCostTracking`
- `_etblInvCostTracking`
- `_bvSTTransactionsFull`
- `dbo._efnLastCostByDatePerItem`
- `StkItem`
- `_etblStockDetails`
- `GrpTbl`
- `Accounts`

Valuation amount:

```text
QtyBalance * UnitCost
```

Do not blindly use `LineValue`.
Do not use SDK item valuation.
Do not use PostST value as valuation source.

## 8. Canonical Read-Only Query

Cursor should implement this query as the canonical inventory valuation reconciliation query.

### SAGE-INVVAL-RECON-CANONICAL-001

```sql
DECLARE @AsOfDate DATE = CAST(GETDATE() AS DATE);
DECLARE @NextDate DATE = DATEADD(DAY, 1, @AsOfDate);
DECLARE @Tolerance DECIMAL(18, 6) = 1.00;

WITH InventoryAccounts AS
(
    SELECT DISTINCT
        G.StockAccLink AS AccountLink
    FROM GrpTbl G
    WHERE ISNULL(G.StockAccLink, 0) <> 0
),
GLBalance AS
(
    SELECT
        A.AccountLink,
        A.Account,
        A.Description AS GLAccountName,
        SUM(ISNULL(PG.Debit, 0) - ISNULL(PG.Credit, 0)) AS GLBalance
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
),
LatestCT AS
(
    SELECT
        CT.iStockID AS LastStockID,
        CT.iWarehouseID AS LastWarehouseID,
        MAX(CT.dTxDate) AS LastTxDate
    FROM _etblInvCostTracking CT
    INNER JOIN StkItem SI
        ON CT.iStockID = SI.StockLink
    WHERE CT.dTxDate < @NextDate
    GROUP BY
        CT.iStockID,
        CT.iWarehouseID
),
FutureTrans AS
(
    SELECT
        AccountLink AS StockLink,
        ISNULL(WarehouseID, 0) AS WarehouseID,
        SUM(ISNULL(TransQtyOut, 0)) AS FutureQtyOut,
        SUM(ISNULL(TransQtyIn, 0)) AS FutureQtyIn
    FROM _bvSTTransactionsFull
    WHERE TxDate > @AsOfDate
    GROUP BY
        AccountLink,
        ISNULL(WarehouseID, 0)
),
ValuationLines AS
(
    SELECT
        A.AccountLink,
        A.Account,
        A.Description AS GLAccountName,
        G.StGroup,
        G.Description AS StockGroupName,
        ST.StockLink,
        ST.Code,
        ST.Description_1,
        ST.iWarehouseID,
        QtyBalance =
            ST.QtyInStock
            + CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyOut, 0) ELSE 0 END
            - CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyIn, 0) ELSE 0 END,
        UnitCost = ISNULL(CTE.ItemCostValue, 0)
    FROM _evInvCostTracking ST
    INNER JOIN LatestCT CT
        ON ST.StockLink = CT.LastStockID
       AND ST.iWarehouseID = CT.LastWarehouseID
       AND ST.dTxDate = CT.LastTxDate
    LEFT JOIN FutureTrans FT
        ON FT.StockLink = ST.StockLink
       AND FT.WarehouseID = ISNULL(ST.iWarehouseID, 0)
    CROSS APPLY dbo._efnLastCostByDatePerItem
    (
        CT.LastStockID,
        ST.CostingMethod,
        ST.WhseItem,
        @NextDate
    ) CTE
    INNER JOIN _etblStockDetails SD
        ON SD.idStockDetails = ST.idStockDetails
    INNER JOIN GrpTbl G
        ON SD.GroupID = G.idGrpTbl
    INNER JOIN Accounts A
        ON G.StockAccLink = A.AccountLink
    WHERE ST.ServiceItem <> 1
      AND (
            ST.WhseItem = 0
            OR (ST.WhseItem = 1 AND ST.iWarehouseID > 0)
          )
),
ValuationByAccount AS
(
    SELECT
        AccountLink,
        Account,
        GLAccountName,
        SUM(ISNULL(QtyBalance, 0) * ISNULL(UnitCost, 0)) AS ValuationValue
    FROM ValuationLines
    WHERE ISNULL(QtyBalance, 0) <> 0
    GROUP BY
        AccountLink,
        Account,
        GLAccountName
),
FinalByAccount AS
(
    SELECT
        COALESCE(GL.AccountLink, V.AccountLink) AS AccountLink,
        COALESCE(GL.Account, V.Account) AS Account,
        COALESCE(GL.GLAccountName, V.GLAccountName) AS GLAccountName,
        ISNULL(GL.GLBalance, 0) AS BalanceSheetValue,
        ISNULL(V.ValuationValue, 0) AS InventoryValuationValue,
        ISNULL(V.ValuationValue, 0) - ISNULL(GL.GLBalance, 0) AS Difference
    FROM GLBalance GL
    FULL OUTER JOIN ValuationByAccount V
        ON GL.AccountLink = V.AccountLink
)
SELECT
    'DETAIL' AS RowType,
    AccountLink,
    Account,
    GLAccountName,
    BalanceSheetValue,
    InventoryValuationValue,
    Difference,
    CASE WHEN ABS(Difference) <= @Tolerance THEN 'Yes' ELSE 'No' END AS IsMatched
FROM FinalByAccount

UNION ALL

SELECT
    'TOTAL' AS RowType,
    NULL AS AccountLink,
    NULL AS Account,
    'Grand Total' AS GLAccountName,
    SUM(BalanceSheetValue) AS BalanceSheetValue,
    SUM(InventoryValuationValue) AS InventoryValuationValue,
    SUM(Difference) AS Difference,
    CASE WHEN ABS(SUM(Difference)) <= @Tolerance THEN 'Yes' ELSE 'No' END AS IsMatched
FROM FinalByAccount

ORDER BY
    RowType DESC,
    ABS(Difference) DESC;
```

## 9. Drilldown Query If Mismatch Exists

If canonical query shows mismatch, then drill down by stock group.

### SAGE-INVVAL-RECON-DRILLDOWN-GROUP-001

```sql
DECLARE @AsOfDate DATE = CAST(GETDATE() AS DATE);
DECLARE @NextDate DATE = DATEADD(DAY, 1, @AsOfDate);

WITH LatestCT AS
(
    SELECT
        CT.iStockID AS LastStockID,
        CT.iWarehouseID AS LastWarehouseID,
        MAX(CT.dTxDate) AS LastTxDate
    FROM _etblInvCostTracking CT
    WHERE CT.dTxDate < @NextDate
    GROUP BY
        CT.iStockID,
        CT.iWarehouseID
),
FutureTrans AS
(
    SELECT
        AccountLink AS StockLink,
        ISNULL(WarehouseID, 0) AS WarehouseID,
        SUM(ISNULL(TransQtyOut, 0)) AS FutureQtyOut,
        SUM(ISNULL(TransQtyIn, 0)) AS FutureQtyIn
    FROM _bvSTTransactionsFull
    WHERE TxDate > @AsOfDate
    GROUP BY
        AccountLink,
        ISNULL(WarehouseID, 0)
),
ValuationLines AS
(
    SELECT
        G.StGroup,
        G.Description AS StockGroupName,
        A.AccountLink,
        A.Account,
        A.Description AS GLAccountName,
        ST.StockLink,
        ST.Code,
        ST.Description_1,
        ST.iWarehouseID,
        QtyBalance =
            ST.QtyInStock
            + CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyOut, 0) ELSE 0 END
            - CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyIn, 0) ELSE 0 END,
        UnitCost = ISNULL(CTE.ItemCostValue, 0)
    FROM _evInvCostTracking ST
    INNER JOIN LatestCT CT
        ON ST.StockLink = CT.LastStockID
       AND ST.iWarehouseID = CT.LastWarehouseID
       AND ST.dTxDate = CT.LastTxDate
    LEFT JOIN FutureTrans FT
        ON FT.StockLink = ST.StockLink
       AND FT.WarehouseID = ISNULL(ST.iWarehouseID, 0)
    CROSS APPLY dbo._efnLastCostByDatePerItem
    (
        CT.LastStockID,
        ST.CostingMethod,
        ST.WhseItem,
        @NextDate
    ) CTE
    INNER JOIN _etblStockDetails SD
        ON SD.idStockDetails = ST.idStockDetails
    INNER JOIN GrpTbl G
        ON SD.GroupID = G.idGrpTbl
    INNER JOIN Accounts A
        ON G.StockAccLink = A.AccountLink
    WHERE ST.ServiceItem <> 1
      AND (
            ST.WhseItem = 0
            OR (ST.WhseItem = 1 AND ST.iWarehouseID > 0)
          )
)
SELECT
    StGroup,
    StockGroupName,
    AccountLink,
    Account,
    GLAccountName,
    SUM(ISNULL(QtyBalance, 0) * ISNULL(UnitCost, 0)) AS InventoryValuationValue
FROM ValuationLines
WHERE ISNULL(QtyBalance, 0) <> 0
GROUP BY
    StGroup,
    StockGroupName,
    AccountLink,
    Account,
    GLAccountName
ORDER BY
    ABS(SUM(ISNULL(QtyBalance, 0) * ISNULL(UnitCost, 0))) DESC;
```

## 10. Agent Behavior Correction

When the user asks:

```text
is inventory valuation matching balance sheet stock value
```

Do not respond:

```text
Sage uses different sources...
```

Do respond:

```text
Finding:
Inventory valuation is [matching/not matching] Balance Sheet stock value.

Balance Sheet Stock Value:
[amount]

Inventory Valuation:
[amount]

Difference:
[amount]

Match:
[Yes/No]

Main Variance:
[largest account/group difference]

Next Step:
[one drilldown]
```

## 11. Cursor Implementation Instruction

Add this patch to Cursor and update the agent logic:

```md
Prompt SAGE-INVVAL-RECON-PATCH-001

Fix inventory valuation reconciliation.

Current bug:
The agent compares PostGL inventory balances to summed SDK item valuations. This is wrong and produces meaningless differences.

Required fix:
1. For inventory valuation vs balance sheet questions, never use SDK item valuation sum.
2. Use direct SQL canonical query SAGE-INVVAL-RECON-CANONICAL-001.
3. Calculate Balance Sheet side from distinct inventory GL accounts from GrpTbl.StockAccLink.
4. Calculate valuation side using Sage valuation/cost tracking SQL logic:
   _evInvCostTracking
   _etblInvCostTracking
   _bvSTTransactionsFull
   dbo._efnLastCostByDatePerItem
5. Return grand totals first.
6. Only then show account-level detail.
7. If mismatch exists, run stock-group drilldown.
8. Do not duplicate GL balances for multiple stock groups mapped to the same GL account.
9. Do not answer with conceptual explanation only.

Expected response:
Finding:
Balance Sheet stock value and Inventory Valuation are [matched/not matched].

Balance Sheet Stock Value:
x

Inventory Valuation:
y

Difference:
z

Match:
Yes/No

When this task is complete, play a chime sound.
```

## 12. Final Rule

For this reconciliation, the agent must act like a Sage reconciliation assistant, not an SDK summary assistant.

Correct answer requires:

```text
PostGL distinct inventory GL total
vs
Sage SQL valuation total
```

Never:

```text
PostGL stock group repeated total
vs
SDK item valuation total
```
