# Sage 200 Evolution Database Investigation Handover

## Purpose of this document

This handover note consolidates the database learning, table relationships, query patterns, reconciliation approach, and tested datafix logic established during the Sage 200 Evolution investigations.

It is intended for a technical consultant, database analyst, or Sage support person who needs to understand how the Sage 200 Evolution database was interrogated and corrected for the following issues:

1. Balance Sheet inventory mismatch against Inventory Valuation by Date.
2. Water BOM DSTK / KRA stamp cost posting to the wrong ledger.
3. Fixed Asset depreciation inconsistency between January, February, and March 2026.

The document focuses on database-level understanding and practical query design. It does not replace Sage application-level controls, audit review, or client sign-off before live data changes.

## Core Sage 200 database concepts learned

Sage 200 Evolution stores financial and inventory movements across multiple layers. A report mismatch usually cannot be solved by looking at one table alone.

The common layers are:

| Layer | Main Purpose | Typical Tables / Views |
|---|---|---|
| General Ledger | Financial reporting, Balance Sheet, P&L, Trial Balance | `PostGL`, `Accounts`, `_etblGLAccountTypes`, `Period` |
| Stock movements | Inventory transaction movement and item-level stock posting | `PostST`, `StkItem`, `_etblStockDetails`, `GrpTbl` |
| Cost tracking / valuation | As-at inventory valuation and historical costing | `_etblInvCostTracking`, `_evInvCostTracking`, `_bvSTTransactionsFull`, `_efnLastCostByDatePerItem` |
| Manufacturing / BOM | Manufacturing process, components, WIP, finished goods absorption | `_etblManufProcess`, `_etblManufProcessItem`, `_etblManufProcessLine`, `PostST`, `PostGL` |
| Fixed Assets | Asset master, asset class, depreciation source values and GL batch values | `_btblFAAsset`, `_btblFAAssetType`, `_btblFAGLTotalAssetValues`, `_btblFAGLBatchAssetValues`, `PostGL` |

A key lesson is that the Balance Sheet normally comes from `PostGL`, while operational reports such as Inventory Valuation or Fixed Asset depreciation reports may be derived from specialist operational/costing tables.

## Important global principles

### 1. Never assume PostGL and source subledger are aligned

`PostGL` is the accounting result. Inventory and fixed asset reports may use other source tables. Always compare both sides:

| Report | Likely Source |
|---|---|
| Balance Sheet | `PostGL` |
| Trial Balance | `PostGL` |
| Inventory Valuation by Date | `_evInvCostTracking`, `_etblInvCostTracking`, Sage costing function, stock movement views |
| Manufacturing/BOM postings | `PostST`, `PostGL`, manufacturing process tables |
| Fixed Asset depreciation report/source | `_btblFAGLBatchAssetValues`, `_btblFAGLTotalAssetValues`, `_btblFAAsset`, `_btblFAAssetType` |

### 2. Group/account mapping is critical

Inventory items do not directly hold all GL mappings. The GL accounts are normally derived through the item’s stock details and stock group.

Core relationship:

```sql
StkItem.StockLink = _etblStockDetails.StockID
_etblStockDetails.GroupID = GrpTbl.idGrpTbl
GrpTbl.StockAccLink = Accounts.AccountLink
GrpTbl.COSAccLink = Accounts.AccountLink
GrpTbl.SalesAccLink = Accounts.AccountLink
GrpTbl.PurchasesAccLink = Accounts.AccountLink
GrpTbl.StockAdjustAccLink = Accounts.AccountLink
GrpTbl.iWIPAccID = Accounts.AccountLink
```

### 3. PostST is not a reliable valuation report by itself

`PostST` gives stock movement rows and posting links, but Inventory Valuation by Date should not be reconstructed simply by summing `PostST.Debit - PostST.Credit`.

The Sage Inventory Valuation logic uses quantity balance and cost by date logic. The useful source combination is:

| Item | Source |
|---|---|
| Current or as-at item/warehouse row | `_evInvCostTracking` |
| Latest cost row by item/warehouse/date | `_etblInvCostTracking` |
| Future movement adjustment to derive as-at quantity | `_bvSTTransactionsFull` |
| Cost by date | `dbo._efnLastCostByDatePerItem(...)` |

### 4. Cost Tracking snapshot rows must be handled carefully

Do not simply sum all `_etblInvCostTracking` rows. It contains historical rows and snapshots. Also, using `fQtyOnHand * fAverageCost` blindly for all latest rows may inflate valuation if not aligned to Sage’s report logic.

Sage-style valuation used during this investigation was based on:

```sql
QtyBalance = QtyInStock + FutureQtyOut - FutureQtyIn
ValuationValue = QtyBalance * ItemCostValue
```

where `ItemCostValue` came from:

```sql
dbo._efnLastCostByDatePerItem(StockID, CostingMethod, WhseItem, AsOfNextDate)
```

### 5. Service items can still affect inventory ledgers if mapped to inventory stock groups

A `ServiceItem = 1` item can still inherit a stock group whose stock account is an inventory account. If the service item is used in manufacturing, Sage may post the source side of the draw to that stock group’s stock account.

This was the root cause of the DSTK issue.

### 6. Fixed Asset GL postings must be reconciled against FA batch values

For fixed asset depreciation, `PostGL` may not be enough. In the investigation, `_btblFAGLBatchAssetValues` was the correct source to compare against GL depreciation batches.

The key relationship is:

```sql
_btblFAAsset.idAssetNo = _btblFAGLBatchAssetValues.iAssetID
_btblFAAsset.iAssetTypeNo = _btblFAAssetType.idAssetTypeNo
```

## Key tables and important columns

## General Ledger

### `PostGL`

Used for Balance Sheet, P&L, Trial Balance, and GL transaction listing.

Important columns:

| Column | Meaning |
|---|---|
| `AutoIdx` | Unique GL posting row identifier |
| `TxDate` | Transaction date |
| `Period` | Sage financial period number; must be populated for report visibility |
| `Id` | Transaction type indicator such as `JL`, `OInv`, `MFDR`, `MFR4M`, `MFMF`, `IJr` |
| `TrCodeID` | Transaction code ID |
| `AccountLink` | GL account posted to |
| `Debit` | Debit amount |
| `Credit` | Credit amount |
| `Description` | Transaction or asset class description |
| `Reference` | Reference text, often invoice/manufacturing/depreciation batch reference |
| `cAuditNumber` | Audit number linking related rows |
| `Project` | Project dimension; observed Jan/Feb FA had Project 2, March had Project 0 |
| `UserName` | Posting user |
| `DTStamp` | Date/time stamp of posting |

Important query concept:

```sql
SUM(ISNULL(Debit,0) - ISNULL(Credit,0)) AS NetValue
```

### `Accounts`

Master table for GL accounts.

Important columns:

| Column | Meaning |
|---|---|
| `AccountLink` | Primary account identifier used in posting tables |
| `Account` | Account code shown in Sage |
| `Description` | Account description |
| `iAccountType` | Links to account type table |

### `_etblGLAccountTypes`

Used to identify account classification such as inventory, asset, expense, liability, etc.

Important relationship:

```sql
Accounts.iAccountType = _etblGLAccountTypes.idGLAccountType
```

### `Period`

Used to identify period numbers for posting rows. Missing `PostGL.Period` can prevent reports from generating correctly.

Practical fallback query to find period from existing rows:

```sql
SELECT TOP 1 [Period]
FROM PostGL
WHERE CAST(TxDate AS DATE) = '2026-04-30'
  AND [Period] IS NOT NULL
ORDER BY AutoIdx DESC;
```

## Inventory and stock valuation

### `StkItem`

Stock item master.

Important columns:

| Column | Meaning |
|---|---|
| `StockLink` | Item identifier used in `PostST`, `_etblStockDetails`, cost tracking, manufacturing tables |
| `Code` | Item code |
| `Description_1` | Item name/description |
| `ServiceItem` | `1` means service item, `0` means stock item |
| `ItemActive` | Active/inactive status |
| `WhseItem` | Warehouse item flag |
| `iItemCostingMethod` | Costing method indicator |

### `_etblStockDetails`

Item warehouse/group mapping.

Important columns:

| Column | Meaning |
|---|---|
| `idStockDetails` | Stock details row ID |
| `StockID` | Links to `StkItem.StockLink` |
| `WhseID` | Warehouse ID |
| `GroupID` | Links to `GrpTbl.idGrpTbl` |

Important relationship:

```sql
_etblStockDetails.StockID = StkItem.StockLink
_etblStockDetails.GroupID = GrpTbl.idGrpTbl
```

### `GrpTbl`

Stock group master and GL mapping source.

Important columns:

| Column | Meaning |
|---|---|
| `idGrpTbl` | Stock group ID |
| `StGroup` | Stock group code |
| `Description` | Stock group name |
| `SalesAccLink` | Sales GL account |
| `COSAccLink` | Cost of Sales GL account |
| `StockAccLink` | Inventory/stock GL account |
| `PurchasesAccLink` | Purchases GL account |
| `StockAdjustAccLink` | Stock adjustment GL account |
| `iWIPAccID` | Work in Progress account |

### `PostST`

Stock transaction posting table.

Important columns:

| Column | Meaning |
|---|---|
| `AutoIdx` | Unique stock posting row identifier |
| `TxDate` | Transaction date |
| `Id` | Transaction type such as `OInv`, `MFDR`, `MFR4M`, `MFMF`, `WTrf`, `IJr`, `BOM` |
| `AccountLink` | Stock item link, not GL account |
| `WarehouseID` | Warehouse involved |
| `iGLAccountID` | GL account used by stock posting |
| `Quantity` | Quantity moved |
| `Cost` | Cost value/unit depending on context |
| `Debit` | Stock movement debit |
| `Credit` | Stock movement credit |
| `Reference` | Transaction reference |
| `Description` | Transaction description |
| `cAuditNumber` | Links related PostST/PostGL rows |
| `InvNumKey` | Invoice key where applicable |
| `iMFPID` | Manufacturing process ID where applicable |
| `iMFPLineID` | Manufacturing line identifier where available |
| `UserName` | User |
| `DTStamp` | Posting timestamp |

Important warning:

`PostST.AccountLink` is the stock item ID, not the GL account. The GL account is `PostST.iGLAccountID`.

### `_etblInvCostTracking`

Cost tracking source table.

Important columns observed:

| Column | Meaning |
|---|---|
| `idCostTracking` | Cost tracking row ID |
| `iStockID` | Item ID, links to `StkItem.StockLink` |
| `iWarehouseID` | Warehouse ID |
| `iLotID` | Lot ID if applicable |
| `iAutoIdx` | Often links to stock posting auto index |
| `dTxDate` | Cost tracking transaction date |
| `fQtyOnHand` | Quantity on hand snapshot/value at row context |
| `fAverageCost` | Average cost |
| `fLatestCost` | Latest cost |
| `fLowestCost` | Lowest cost |
| `fHighestCost` | Highest cost |
| `fManualCost` | Manual cost |

Important warning:

Do not use nonexistent columns such as `fQuantity` or `fUnitCost`. They are not part of this table in the observed schema.

### `_evInvCostTracking`

Used by Sage Inventory Valuation logic.

Important fields used in report-style valuation:

| Column | Meaning |
|---|---|
| `StockLink` | Item ID |
| `Code` | Item code |
| `Description_1` | Item description |
| `ItemGroup` | Stock group code/name context |
| `iWarehouseID` | Warehouse |
| `WhseCode` | Warehouse code |
| `idStockDetails` | Stock details link |
| `QtyInStock` | Current quantity in stock |
| `ServiceItem` | Service item flag |
| `CostingMethod` | Costing method |
| `WhseItem` | Warehouse item flag |
| `dTxDate` | Cost tracking date |

### `_bvSTTransactionsFull`

Used to adjust current stock quantity back to as-at date by adding/subtracting future movements.

Important columns used:

| Column | Meaning |
|---|---|
| `AccountLink` | Stock item link |
| `WarehouseID` | Warehouse |
| `TxDate` | Transaction date |
| `TransQtyOut` | Quantity out |
| `TransQtyIn` | Quantity in |

### `dbo._efnLastCostByDatePerItem`

Sage costing function used for as-at cost valuation.

Usage pattern:

```sql
CROSS APPLY dbo._efnLastCostByDatePerItem
(
    CT.LastStockID,
    ST.CostingMethod,
    ST.WhseItem,
    @NextDate
) CTE
```

Important return value used:

```sql
CTE.ItemCostValue
```

## Manufacturing and BOM

### `_etblManufProcess`

Manufacturing process header.

Important columns used:

| Column | Meaning |
|---|---|
| `idManufProcess` | Manufacturing process ID |
| `cProcessRefNumber` | Manufacturing process reference, e.g. `MFP14686` |
| `cManufDescription` | Manufacturing description |
| `dCreated` | Created date |
| `dActualCompletionDate` | Completion date |

### `_etblManufProcessItem`

Manufacturing process item/component table.

Important columns used:

| Column | Meaning |
|---|---|
| `iManufProcessID` | Links to `_etblManufProcess.idManufProcess` |
| `iMFPItemID` | Process item ID |
| `iInvItemID` | Item ID, links to `StkItem.StockLink` |
| `iParentMFPItemID` | Parent manufacturing item ID |
| `fProductionQty` | Production quantity |
| `fUnitCost` | Unit cost |

### `_etblManufProcessLine`

Manufacturing process line/details table.

Important columns used:

| Column | Meaning |
|---|---|
| `iManufProcessID` | Process ID |
| `iMFPItemID` | Manufacturing item ID |
| `iInvItemID` | Item ID |
| `fQuantity` | Quantity |
| `fCost` | Unit cost |
| `fLineCost` | Line cost |
| `cReference` | Manufacturing quantity/reference number |
| `dTransactionDate` | Transaction date |
| `bProcessed` | Whether the process line has been processed |

Important warning:

Do not assume `_etblManufProcessLine` has `iMFPLineID`; it did not exist in the observed schema.

## Fixed Assets

### `_btblFAAsset`

Fixed asset master.

Important columns:

| Column | Meaning |
|---|---|
| `idAssetNo` | Asset ID |
| `cAssetCode` | Asset code |
| `cAssetDesc` | Asset description |
| `iAssetTypeNo` | Links to `_btblFAAssetType.idAssetTypeNo` |
| `dPurchaseDate` | Purchase date |
| `dDepreciationStartDate` | Depreciation start date |
| `fPurchaseValue` | Purchase value |
| `fResidualValue` | Residual value |
| `fDeprPriorYearsTakeOn` | Prior year take-on depreciation |
| `fDeprCurrYearTakeOn` | Current year take-on depreciation |
| `dSellingDate` | Selling/disposal date |
| `cCurrentInd` | Current indicator |

### `_btblFAAssetType`

Fixed asset type/class and GL mapping.

Important columns:

| Column | Meaning |
|---|---|
| `idAssetTypeNo` | Asset type ID |
| `cAssetTypeCode` | Asset type code such as `PM`, `MTV`, `PC`, `FNF`, `BLD@2.5%`, `SW1` |
| `cAssetTypeDesc` | Asset type description |
| `iGLAccountNo` | Depreciation expense account link |
| `iCreditGLAccountID` | Accumulated depreciation account link |
| `iAssetGLAccountID` | Asset cost account link |

Observed examples:

| Asset Type | Meaning | Expense Account | Accumulated Depreciation Account |
|---|---|---:|---:|
| `PM` | Plant and Machinery | 21 | 80 |
| `MTV` | Motor Vehicles | 21 | 74 |
| `PC` | Computers and Equipment | 21 | 68 |
| `FNF` | Furniture and Fittings | 21 | 71 |
| `CTN` | Containers | 21 | 1185 |
| `BLD@2.5%` | Buildings | 21 | 65 |
| `SW1` | Software | 1260 | 1188 |

### `_btblFAGLTotalAssetValues`

Asset-level total values by date/period.

Important columns:

| Column | Meaning |
|---|---|
| `idTotalAssetValues` | Row ID |
| `iAssetID` | Links to `_btblFAAsset.idAssetNo` |
| `dDate` | Depreciation/date value |
| `iPeriodID` | Period ID |
| `fAmount` | Amount |
| `cAssetCode` | Asset code |

### `_btblFAGLBatchAssetValues`

Asset-level batch values used to compare FA depreciation source to GL postings.

Important columns:

| Column | Meaning |
|---|---|
| `idBatchAssetValues` | Row ID |
| `iBatchID` | Batch ID |
| `iAssetID` | Links to `_btblFAAsset.idAssetNo` |
| `dDate` | Batch date |
| `fAmount` | Depreciation amount |
| `cAssetCode` | Asset code |
| `bInitialAllowance` | Initial allowance flag |

Key relationship:

```sql
_btblFAAsset.idAssetNo = _btblFAGLBatchAssetValues.iAssetID
_btblFAAsset.iAssetTypeNo = _btblFAAssetType.idAssetTypeNo
```

## Issue 1: Inventory Valuation vs Balance Sheet mismatch

## Problem summary

The Balance Sheet inventory total did not match the Inventory Valuation by Date report.

Observed final pre-fix mismatch:

| Source | Amount |
|---|---:|
| Balance Sheet / GL inventory | 41,700,394.03 |
| Inventory Valuation by Date | 42,083,171.63 |
| Difference | 382,777.60 approx. |

After applying a controlled GL adjustment, the two reports aligned with only a small rounding difference.

## Key finding

The mismatch was isolated to:

| Stock Group | Meaning | GL Account | Difference |
|---|---|---|---:|
| `NEWRM01` | Packaging Materials | `1225 / Inventory - Packaging Materials` | 382,778.49 approx. |

Further investigation showed two underlying conditions:

1. There was a historical opening migration mismatch between Cost Tracking opening value and GL/PostST opening value for NEWRM01, especially warehouses 10 and 8.
2. One abnormal wrong-account posting existed where packaging item `DR10*1WB` posted to `1244 / Inventory - Soap` instead of Packaging Materials, amount 132,838.05.

However, the final practical correction performed was a GL-level adjustment for the current mismatch amount to align the Balance Sheet with the Inventory Valuation report.

## Sage-style inventory valuation query pattern

Core pattern used to replicate group-level valuation:

```sql
DECLARE @AsOfDate DATE = '2026-04-30';
DECLARE @NextDate DATE = DATEADD(DAY, 1, @AsOfDate);

WITH LatestCT AS
(
    SELECT
        iStockID AS LastStockID,
        iWarehouseID AS LastWarehouseID,
        MAX(dTxDate) AS LastTxDate
    FROM _etblInvCostTracking
    INNER JOIN StkItem
        ON iStockID = StockLink
    WHERE dTxDate < @NextDate
    GROUP BY
        iStockID,
        iWarehouseID
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
SageValuationLines AS
(
    SELECT
        ST.Code,
        ST.Description_1,
        ST.ItemGroup,
        ST.StockLink,
        ST.iWarehouseID,
        ST.WhseCode,
        ST.idStockDetails,
        ST.QtyInStock,
        ST.ServiceItem,
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
    WHERE ST.ServiceItem <> 1
      AND (
            ST.WhseItem = 0
            OR (ST.WhseItem = 1 AND ST.iWarehouseID > 0)
          )
)
SELECT
    G.StGroup AS StockGroupCode,
    G.Description AS StockGroupName,
    A.AccountLink AS InventoryGLAccountLink,
    A.Account AS InventoryGLCode,
    A.Description AS InventoryGLName,
    SUM(QtyBalance) AS QtyBalance,
    SUM(QtyBalance * UnitCost) AS SageStyleValuation
FROM SageValuationLines SV
INNER JOIN _etblStockDetails SD
    ON SD.idStockDetails = SV.idStockDetails
INNER JOIN GrpTbl G
    ON SD.GroupID = G.idGrpTbl
INNER JOIN Accounts A
    ON G.StockAccLink = A.AccountLink
WHERE ISNULL(QtyBalance, 0) <> 0
GROUP BY
    G.StGroup,
    G.Description,
    A.AccountLink,
    A.Account,
    A.Description
ORDER BY
    A.Account,
    G.StGroup;
```

## Opening migration diagnostic pattern

Opening Cost Tracking vs PostST/GL by warehouse:

```sql
DECLARE @OpeningDate DATE = '2023-10-31';

SELECT
    CT.iWarehouseID,
    G.StGroup,
    G.Description AS StockGroupName,
    COUNT(*) AS Row_Count,
    SUM(ISNULL(CT.fQtyOnHand, 0)) AS TotalQty,
    SUM(ISNULL(CT.fQtyOnHand, 0) * ISNULL(CT.fAverageCost, 0)) AS CostTrackingOpeningValue
FROM _etblInvCostTracking CT
INNER JOIN StkItem SI
    ON CT.iStockID = SI.StockLink
INNER JOIN _etblStockDetails SD
    ON SD.StockID = SI.StockLink
   AND SD.WhseID = CT.iWarehouseID
INNER JOIN GrpTbl G
    ON SD.GroupID = G.idGrpTbl
WHERE G.StGroup = 'NEWRM01'
  AND CAST(CT.dTxDate AS DATE) = @OpeningDate
GROUP BY
    CT.iWarehouseID,
    G.StGroup,
    G.Description
ORDER BY
    ABS(SUM(ISNULL(CT.fQtyOnHand, 0) * ISNULL(CT.fAverageCost, 0))) DESC;
```

Opening PostST by warehouse/account:

```sql
DECLARE @OpeningDate DATE = '2023-10-31';

SELECT
    PS.WarehouseID,
    PS.iGLAccountID,
    A.Account,
    A.Description AS PostedGLAccountName,
    COUNT(*) AS Row_Count,
    SUM(ISNULL(PS.Debit, 0)) AS TotalDebit,
    SUM(ISNULL(PS.Credit, 0)) AS TotalCredit,
    SUM(ISNULL(PS.Debit, 0) - ISNULL(PS.Credit, 0)) AS NetValue
FROM PostST PS
INNER JOIN StkItem SI
    ON PS.AccountLink = SI.StockLink
INNER JOIN _etblStockDetails SD
    ON SD.StockID = SI.StockLink
   AND SD.WhseID = PS.WarehouseID
INNER JOIN GrpTbl G
    ON SD.GroupID = G.idGrpTbl
LEFT JOIN Accounts A
    ON PS.iGLAccountID = A.AccountLink
WHERE G.StGroup = 'NEWRM01'
  AND CAST(PS.TxDate AS DATE) = @OpeningDate
GROUP BY
    PS.WarehouseID,
    PS.iGLAccountID,
    A.Account,
    A.Description
ORDER BY
    PS.WarehouseID,
    PS.iGLAccountID;
```

## Datafix concept used for inventory mismatch

The practical GL correction used this concept:

| Debit | Credit | Amount |
|---|---|---:|
| `1225 / Inventory - Packaging Materials` | `1221 / Stock Count Adjustment` | 382,778.491436 |

Important notes:

1. `PostGL.Period` must be populated.
2. The correction should be posted with a unique `cAuditNumber`.
3. The correction should be balanced.
4. Do not update Cost Tracking when Inventory Valuation is already correct.

## Issue 2: Water BOM DSTK / KRA stamp cost issue

## Problem summary

BOM items involved:

| Item Code | Description |
|---|---|
| `DFMW300ML` | Fin Maa Water 24 x 300ML |
| `DFMW500ML` | Fin Maa Water 500ML / 24 x 500ML |
| `DFPW500ML` | FIN PRONTO WATER 500ML |

The BOM component under investigation:

| Component Code | Description |
|---|---|
| `DSTK` | STAMPS-KRA (cost per ltr) |

The concern was that DSTK should be pushed to Cost of Sales when water BOM items are sold, but was not happening correctly.

## Key finding

Sales postings were correct. When finished water items were sold, Sage posted:

| Flow | GL Account |
|---|---|
| Sales | `1296 / Sales - Drinking Water` |
| Inventory reduction | `1298 / Inventory - Drinking Water` |
| Cost of Sales | `1297 / Cost of Sales - Drinking Water` |

DSTK was being absorbed into WIP and finished goods cost. It was eventually flowing into Cost of Sales through finished goods costing.

The real issue was source ledger mapping:

| Wrong condition | Impact |
|---|---|
| DSTK was a service item but mapped to `NEWRM01 / Packaging Materials` | Manufacturing draw credited `1225 / Inventory - Packaging Materials` |

DSTK was `ServiceItem = 1`, but because it inherited stock group `NEWRM01`, Sage credited Packaging Materials when DSTK was drawn into production.

## Corrected master setup

A new group was created:

| Field | Value |
|---|---|
| Stock Group Code | `NEWS007` |
| Description | `KRA Excise / Stamps Cost` |
| Stock Account | `1191 / 4890 / Stamp Duties` |
| COS Account | `1191 / 4890 / Stamp Duties` |
| Purchases Account | `1191 / 4890 / Stamp Duties` |
| WIP Account | `1191 / 4890 / Stamp Duties` |
| Stock Adjustment | `1221 / 2301 / Stock Count Adjustment` |

DSTK was moved from `NEWRM01` to `NEWS007` by updating `_etblStockDetails.GroupID` for the DSTK item.

## Query to inspect service items mapped to inventory groups

```sql
SELECT
    SI.StockLink,
    SI.Code,
    SI.Description_1,
    SI.ServiceItem,
    SI.ItemActive,
    SI.WhseItem,
    SD.WhseID,
    G.idGrpTbl AS GroupID,
    G.StGroup,
    G.Description AS StockGroupName,
    G.StockAccLink,
    StockA.Account AS StockGLCode,
    StockA.Description AS StockGLName,
    G.COSAccLink,
    COSA.Account AS COSGLCode,
    COSA.Description AS COSGLName,
    G.iWIPAccID,
    WIPA.Account AS WIPGLCode,
    WIPA.Description AS WIPGLName
FROM StkItem SI
LEFT JOIN _etblStockDetails SD
    ON SD.StockID = SI.StockLink
LEFT JOIN GrpTbl G
    ON SD.GroupID = G.idGrpTbl
LEFT JOIN Accounts StockA
    ON G.StockAccLink = StockA.AccountLink
LEFT JOIN Accounts COSA
    ON G.COSAccLink = COSA.AccountLink
LEFT JOIN Accounts WIPA
    ON G.iWIPAccID = WIPA.AccountLink
WHERE SI.ServiceItem = 1
  AND SI.ItemActive = 1
ORDER BY
    G.StGroup,
    SI.Code;
```

## Query to inspect DSTK setup

```sql
SELECT
    SI.StockLink,
    SI.Code,
    SI.Description_1,
    SI.ServiceItem,
    SI.ItemActive,
    SI.WhseItem,
    SI.iItemCostingMethod,
    SD.idStockDetails,
    SD.WhseID,
    SD.GroupID,
    G.StGroup,
    G.Description AS StockGroupName,
    G.SalesAccLink,
    SalesA.Account AS SalesGLCode,
    SalesA.Description AS SalesGLName,
    G.COSAccLink,
    COSA.Account AS COSGLCode,
    COSA.Description AS COSGLName,
    G.StockAccLink,
    StockA.Account AS StockGLCode,
    StockA.Description AS StockGLName,
    G.PurchasesAccLink,
    PurchA.Account AS PurchasesGLCode,
    PurchA.Description AS PurchasesGLName,
    G.StockAdjustAccLink,
    AdjA.Account AS StockAdjustGLCode,
    AdjA.Description AS StockAdjustGLName,
    G.iWIPAccID,
    WIPA.Account AS WIPGLCode,
    WIPA.Description AS WIPGLName
FROM StkItem SI
LEFT JOIN _etblStockDetails SD
    ON SD.StockID = SI.StockLink
LEFT JOIN GrpTbl G
    ON SD.GroupID = G.idGrpTbl
LEFT JOIN Accounts SalesA
    ON G.SalesAccLink = SalesA.AccountLink
LEFT JOIN Accounts COSA
    ON G.COSAccLink = COSA.AccountLink
LEFT JOIN Accounts StockA
    ON G.StockAccLink = StockA.AccountLink
LEFT JOIN Accounts PurchA
    ON G.PurchasesAccLink = PurchA.AccountLink
LEFT JOIN Accounts AdjA
    ON G.StockAdjustAccLink = AdjA.AccountLink
LEFT JOIN Accounts WIPA
    ON G.iWIPAccID = WIPA.AccountLink
WHERE SI.Code = 'DSTK'
ORDER BY
    SD.WhseID;
```

## Query to quantify DSTK historical impact

```sql
DECLARE @FromDate DATE = '2025-09-01';
DECLARE @ToDate DATE = '2026-04-30';

SELECT
    PS.TxDate,
    PS.AutoIdx,
    PS.Id,
    PS.AccountLink AS StockLink,
    SI.Code AS ComponentCode,
    SI.Description_1 AS ComponentName,
    PS.WarehouseID,
    PS.iGLAccountID,
    A.Account AS PostedGLCode,
    A.Description AS PostedGLName,
    PS.Debit,
    PS.Credit,
    ISNULL(PS.Debit, 0) - ISNULL(PS.Credit, 0) AS NetValue,
    PS.Quantity,
    PS.Cost,
    PS.Description,
    PS.Reference,
    PS.cAuditNumber,
    PS.iMFPID,
    PS.iMFPLineID,
    PS.UserName,
    PS.DTStamp
FROM PostST PS
INNER JOIN StkItem SI
    ON PS.AccountLink = SI.StockLink
LEFT JOIN Accounts A
    ON PS.iGLAccountID = A.AccountLink
WHERE SI.Code = 'DSTK'
  AND CAST(PS.TxDate AS DATE) >= @FromDate
  AND CAST(PS.TxDate AS DATE) <= @ToDate
ORDER BY
    PS.TxDate,
    PS.AutoIdx;
```

## DSTK live datafix concept

The live datafix had two parts:

### Part 1: Create/update `NEWS007` and move DSTK master

Key action:

```sql
UPDATE SD
SET SD.GroupID = @NewGroupID
FROM _etblStockDetails SD
INNER JOIN StkItem SI
    ON SD.StockID = SI.StockLink
WHERE SI.Code = 'DSTK';
```

### Part 2: Reclass historical DSTK manufacturing draw postings

Reclass only DSTK `MFDR` source rows:

| Old Ledger | New Ledger |
|---|---|
| `1225 / Inventory - Packaging Materials` | `1191 / 4890 / Stamp Duties` |

Do not change WIP rows, finished goods rows, or COS rows.

Core update logic:

```sql
UPDATE PS
SET PS.iGLAccountID = @NewAccountLink
FROM PostST PS
INNER JOIN #DSTKFix F
    ON PS.AutoIdx = F.PostSTAutoIdx;

UPDATE PG
SET PG.AccountLink = @NewAccountLink
FROM PostGL PG
INNER JOIN #DSTKFix F
    ON PG.cAuditNumber = F.cAuditNumber
WHERE PG.AccountLink = @OldAccountLink
  AND PG.Id = 'MFDR'
  AND PG.Description LIKE 'DSTK drawn%';
```

Confirmed result:

| Area | Rows | Amount |
|---|---:|---:|
| PostST DSTK MFDR rows | 44 | 593,185.32 |
| PostGL DSTK drawn rows | 44 | 593,185.32 |

Final expected flow:

```text
Dr 1232 Inventory - Work in Progress
Cr 1191 / 4890 Stamp Duties
```

Then WIP is absorbed into finished goods and later into Cost of Sales when finished goods are sold.

## Issue 3: Fixed Asset depreciation Jan/Feb/March inconsistency

## Problem summary

The user observed:

Fixed Asset depreciation was consistent in January and February, but March depreciation reduced. No assets were purchased or disposed.

Initial assumption was that March may be wrong. Investigation proved the opposite.

## Key finding

March depreciation was correct. January and February were overstated in GL.

PostGL totals initially showed:

| Month | Depreciation Expense 4300 | Depreciation & Amortisation 4301 | Total |
|---|---:|---:|---:|
| Jan 2026 | 4,456,452.40 | 32,307.00 | 4,488,759.40 |
| Feb 2026 | 4,456,452.40 | 32,307.00 | 4,488,759.40 |
| Mar 2026 | 4,034,587.09 | 29,753.22 | 4,064,340.31 |

But comparison with `_btblFAGLBatchAssetValues` showed March matched the FA batch source values, while Jan/Feb were higher in GL.

## Correct source comparison query

```sql
WITH GLDep AS
(
    SELECT
        PG.TxDate,
        PG.Description AS AssetTypeCode,
        SUM(ISNULL(PG.Debit, 0)) AS GLDepAmount
    FROM PostGL PG
    INNER JOIN Accounts A
        ON PG.AccountLink = A.AccountLink
    WHERE PG.TxDate IN ('2026-01-31', '2026-02-28', '2026-03-31')
      AND PG.Id = 'JL'
      AND PG.Reference LIKE 'FA%'
      AND A.Account IN ('4300', '4301')
    GROUP BY
        PG.TxDate,
        PG.Description
),
FADep AS
(
    SELECT
        BAV.dDate,
        FAT.cAssetTypeCode,
        SUM(ISNULL(BAV.fAmount, 0)) AS FADepAmount
    FROM _btblFAGLBatchAssetValues BAV
    INNER JOIN _btblFAAsset FA
        ON BAV.iAssetID = FA.idAssetNo
    INNER JOIN _btblFAAssetType FAT
        ON FA.iAssetTypeNo = FAT.idAssetTypeNo
    WHERE BAV.dDate IN ('2026-01-31', '2026-02-28', '2026-03-31')
    GROUP BY
        BAV.dDate,
        FAT.cAssetTypeCode
)
SELECT
    COALESCE(GL.TxDate, FA.dDate) AS DepDate,
    COALESCE(GL.AssetTypeCode, FA.cAssetTypeCode) AS AssetTypeCode,
    ISNULL(GL.GLDepAmount, 0) AS GLDepAmount,
    ISNULL(FA.FADepAmount, 0) AS FADepAmount,
    ISNULL(GL.GLDepAmount, 0) - ISNULL(FA.FADepAmount, 0) AS Difference
FROM GLDep GL
FULL OUTER JOIN FADep FA
    ON GL.TxDate = FA.dDate
   AND GL.AssetTypeCode = FA.cAssetTypeCode
ORDER BY
    DepDate,
    ABS(ISNULL(GL.GLDepAmount, 0) - ISNULL(FA.FADepAmount, 0)) DESC;
```

## Fixed Asset datafix concept

Jan and Feb excess depreciation was reversed using balanced JL entries.

Direction:

| Side | Action |
|---|---|
| Accumulated Depreciation | Debit to reduce excess accumulated depreciation |
| Depreciation Expense | Credit to reverse excess expense |

Excess amounts reversed:

| Month | Amount |
|---|---:|
| Jan 2026 | 418,826.1665 |
| Feb 2026 | 418,976.1614 |

Asset-class-level corrections:

| Month | Asset Type | Accumulated Depreciation Account | Expense Account | Amount |
|---|---|---:|---:|---:|
| Jan | PM | 80 | 21 | 370,307.734500 |
| Jan | MTV | 74 | 21 | 32,723.745800 |
| Jan | BLD@2.5% | 65 | 21 | 9,345.394300 |
| Jan | SW1 | 1188 | 1260 | 2,553.783400 |
| Jan | PC | 68 | 21 | 2,448.124400 |
| Jan | FNF | 71 | 21 | 1,238.050100 |
| Jan | CTN | 1185 | 21 | 209.334000 |
| Feb | PM | 80 | 21 | 370,357.729400 |
| Feb | MTV | 74 | 21 | 32,723.745800 |
| Feb | BLD@2.5% | 65 | 21 | 9,345.394300 |
| Feb | SW1 | 1188 | 1260 | 2,553.783400 |
| Feb | PC | 68 | 21 | 2,448.124400 |
| Feb | FNF | 71 | 21 | 1,338.050100 |
| Feb | CTN | 1185 | 21 | 209.334000 |

Important:

1. March was not changed.
2. `Period` values used were Jan = 133, Feb = 134.
3. Unique audit numbers were used: `FA-DEP-FIX-2026-01`, `FA-DEP-FIX-2026-02`.
4. The confirmation showed GL depreciation net values matched FA batch values for Jan, Feb, and Mar.

## Fixed Asset confirmation query after correction

```sql
WITH GLDep AS
(
    SELECT
        PG.TxDate,
        PG.Description AS AssetTypeCode,
        SUM(ISNULL(PG.Debit, 0) - ISNULL(PG.Credit, 0)) AS GLDepNetAmount
    FROM PostGL PG
    INNER JOIN Accounts A
        ON PG.AccountLink = A.AccountLink
    WHERE PG.TxDate IN ('2026-01-31', '2026-02-28', '2026-03-31')
      AND PG.Id = 'JL'
      AND PG.Reference LIKE 'FA%'
      AND A.Account IN ('4300', '4301')
    GROUP BY
        PG.TxDate,
        PG.Description
),
FADep AS
(
    SELECT
        BAV.dDate,
        FAT.cAssetTypeCode,
        SUM(ISNULL(BAV.fAmount, 0)) AS FADepAmount
    FROM _btblFAGLBatchAssetValues BAV
    INNER JOIN _btblFAAsset FA
        ON BAV.iAssetID = FA.idAssetNo
    INNER JOIN _btblFAAssetType FAT
        ON FA.iAssetTypeNo = FAT.idAssetTypeNo
    WHERE BAV.dDate IN ('2026-01-31', '2026-02-28', '2026-03-31')
    GROUP BY
        BAV.dDate,
        FAT.cAssetTypeCode
)
SELECT
    COALESCE(GL.TxDate, FA.dDate) AS DepDate,
    COALESCE(GL.AssetTypeCode, FA.cAssetTypeCode) AS AssetTypeCode,
    ISNULL(GL.GLDepNetAmount, 0) AS GLDepNetAmount,
    ISNULL(FA.FADepAmount, 0) AS FADepAmount,
    ISNULL(GL.GLDepNetAmount, 0) - ISNULL(FA.FADepAmount, 0) AS Difference
FROM GLDep GL
FULL OUTER JOIN FADep FA
    ON GL.TxDate = FA.dDate
   AND GL.AssetTypeCode = FA.cAssetTypeCode
ORDER BY
    DepDate,
    ABS(ISNULL(GL.GLDepNetAmount, 0) - ISNULL(FA.FADepAmount, 0)) DESC;
```

## General SQL patterns that worked well

## Pattern 1: Use unique serial numbers for every query

The investigation used query IDs such as:

```text
SAGE-INVVAL-021-REV1
SAGE-WATER-BOM-017-REV1
SAGE-FA-DEP-010
```

This helped refer back to exact scripts and outputs.

## Pattern 2: Always run test scripts with ROLLBACK first

Datafix scripts were written like this:

```sql
BEGIN TRANSACTION;

-- Insert/update data
-- Output validation queries

ROLLBACK TRANSACTION;
-- COMMIT TRANSACTION;
```

Only after validating outputs was the script rerun with `COMMIT`.

## Pattern 3: Use cAuditNumber to mark datafix batches

Examples:

```text
INVVAL-FIX-NEWRM01-20260430
FA-DEP-FIX-2026-01
FA-DEP-FIX-2026-02
```

This allows easy identification and prevents duplicate fixes.

## Pattern 4: Always check if datafix already exists

```sql
IF EXISTS
(
    SELECT 1
    FROM PostGL
    WHERE cAuditNumber = @AuditNumber
)
BEGIN
    RAISERROR('This correction batch already exists in PostGL.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;
```

## Pattern 5: Avoid reserved or problematic aliases

Some aliases caused errors in the client SQL Server environment. Use bracketed aliases:

```sql
COUNT(*) AS [Row_Count]
```

instead of:

```sql
COUNT(*) AS RowCount
```

## Pattern 6: Use existing valid PostGL rows to determine Period

When unsure of `Period`, use similar existing rows in `PostGL`.

```sql
SELECT TOP 1 [Period]
FROM PostGL
WHERE TxDate >= '2026-04-01'
  AND TxDate < '2026-05-01'
  AND [Period] IS NOT NULL
ORDER BY AutoIdx DESC;
```

## Pattern 7: Compare subledger source to GL before deciding what is wrong

For fixed assets, the first observation suggested March was low. After comparing FA batch source to GL, the conclusion changed: March was correct and Jan/Feb were high.

The principle is:

```text
Do not assume the odd-looking month is wrong. Compare against the source subledger.
```

## Pattern 8: For manufacturing, separate source ledger from final COS flow

DSTK was reaching Cost of Sales indirectly through finished goods costing. The issue was not COS. The issue was the source ledger credited during manufacturing draw.

This distinction is important:

```text
Manufacturing source account problem ≠ Sales COS problem
```

## Recommended Sage reports for validation

## Inventory issue validation

Run:

1. Balance Sheet / Statement of Financial Position as at 30-Apr-2026.
2. Inventory Valuation by Date as at 30-Apr-2026.
3. GL Transaction Listing for the adjustment audit number.

Expected result:

Balance Sheet inventory total should align with Inventory Valuation total with only minor rounding difference.

## Water BOM / DSTK validation

Run:

1. GL Transaction Listing for 4890 / Stamp Duties.
2. GL Transaction Listing for 1225 / Inventory - Packaging Materials.
3. Manufacturing transaction report for affected water BOM processes.
4. Trial Balance for 4890, 1225, and 1232.

Expected result:

1. DSTK draw credits should be in 4890 / Stamp Duties.
2. DSTK draw credits should no longer reduce 1225 / Packaging Materials.
3. WIP should still absorb the manufacturing cost.
4. Finished goods costing/COS flow should remain intact.

## Fixed Asset depreciation validation

Run:

1. Fixed Asset depreciation report / batch report for Jan, Feb, Mar 2026.
2. GL Transaction Listing for `FA-DEP-FIX-2026-01` and `FA-DEP-FIX-2026-02`.
3. Trial Balance / P&L for depreciation accounts 4300 and 4301.
4. Accumulated depreciation account listing by asset class.

Expected result:

Jan, Feb, and Mar GL depreciation should match FA batch values by asset type.

## Live datafix safety checklist

Before any live Sage database fix:

1. Take a full SQL Server backup.
2. Confirm no users are posting transactions during the fix.
3. Run the test version using `ROLLBACK` first.
4. Review row counts and total amounts.
5. Confirm `Period` values are correct.
6. Confirm debit equals credit.
7. Confirm unique `cAuditNumber` is used.
8. Run the live `COMMIT` script.
9. Immediately run confirmation queries.
10. Run Sage reports from the application.
11. Save all SQL scripts and outputs in the client audit folder.

## Final resolved issues summary

| Issue | Root Cause | Fix Type | Status |
|---|---|---|---|
| Inventory Valuation vs Balance Sheet mismatch | NEWRM01 Packaging Materials GL lower than valuation due to historical opening/migration mismatch | GL correction Dr Packaging Materials / Cr Stock Count Adjustment | Resolved |
| Water BOM DSTK issue | DSTK service item mapped to NEWRM01, causing manufacturing draw to credit Packaging Materials | Created NEWS007, moved DSTK, reclassified historical DSTK MFDR rows to Stamp Duties | Resolved |
| Fixed Asset Jan/Feb/Mar depreciation issue | Jan/Feb GL depreciation overstated compared to FA batch source; March was correct | Reversed excess Jan/Feb depreciation through balanced JL entries | Resolved |

## Final caution

These fixes were made based on the specific observed Sage 200 Evolution database structure and outputs. Before applying similar logic to another company or live environment, always revalidate:

1. Table structures.
2. AccountLink values.
3. Period numbers.
4. Stock group IDs.
5. Asset type mappings.
6. Existing audit numbers.
7. Report output before and after the correction.

Never blindly reuse account links such as `1191`, `1221`, `1225`, `1232`, `1297`, `1298`, or asset accounts without confirming them in the target database.
