using System.Data;
using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>
/// Inventory valuation vs Balance Sheet — canonical Sage SQL (not SDK item sum).
/// DOCS/Sage_AI_Inventory_Reconciliation_Patch.md — SAGE-INVVAL-RECON-CANONICAL-001.
/// </summary>
internal static class InventoryGlReconcileHandler
{
    public const string QuerySerial = "SAGE-INVVAL-RECON-CANONICAL-001";

    private const decimal DefaultTolerance = 1.00m;

    private const string CanonicalSql = """
        DECLARE @AsOfDate DATE = @pAsOfDate;
        DECLARE @NextDate DATE = DATEADD(DAY, 1, @AsOfDate);
        DECLARE @Tolerance DECIMAL(18, 6) = @pTolerance;

        WITH InventoryAccounts AS
        (
            SELECT DISTINCT G.StockAccLink AS AccountLink
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
            INNER JOIN Accounts A ON IA.AccountLink = A.AccountLink
            LEFT JOIN PostGL PG ON PG.AccountLink = A.AccountLink
                AND CAST(PG.TxDate AS DATE) <= @AsOfDate
            GROUP BY A.AccountLink, A.Account, A.Description
        ),
        LatestCT AS
        (
            SELECT
                CT.iStockID AS LastStockID,
                CT.iWarehouseID AS LastWarehouseID,
                MAX(CT.dTxDate) AS LastTxDate
            FROM _etblInvCostTracking CT
            INNER JOIN StkItem SI ON CT.iStockID = SI.StockLink
            WHERE CT.dTxDate < @NextDate
            GROUP BY CT.iStockID, CT.iWarehouseID
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
            GROUP BY AccountLink, ISNULL(WarehouseID, 0)
        ),
        ValuationLines AS
        (
            SELECT
                A.AccountLink,
                A.Account,
                A.Description AS GLAccountName,
                ST.StockLink,
                ST.Code,
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
            CROSS APPLY dbo._efnLastCostByDatePerItem(
                CT.LastStockID, ST.CostingMethod, ST.WhseItem, @NextDate) CTE
            INNER JOIN _etblStockDetails SD ON SD.idStockDetails = ST.idStockDetails
            INNER JOIN GrpTbl G ON SD.GroupID = G.idGrpTbl
            INNER JOIN Accounts A ON G.StockAccLink = A.AccountLink
            WHERE ST.ServiceItem <> 1
              AND (ST.WhseItem = 0 OR (ST.WhseItem = 1 AND ST.iWarehouseID > 0))
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
            GROUP BY AccountLink, Account, GLAccountName
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
            FULL OUTER JOIN ValuationByAccount V ON GL.AccountLink = V.AccountLink
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
            NULL, NULL, 'Grand Total',
            SUM(BalanceSheetValue),
            SUM(InventoryValuationValue),
            SUM(Difference),
            CASE WHEN ABS(SUM(Difference)) <= @Tolerance THEN 'Yes' ELSE 'No' END
        FROM FinalByAccount
        ORDER BY CASE WHEN RowType = 'TOTAL' THEN 1 ELSE 0 END, ABS(Difference) DESC;
        """;

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var asOfDate = ParseAsOfDate(parameters);
        var tolerance = DefaultTolerance;

        try
        {
            var (total, accounts) = RunCanonicalQuery(companyConnectionString, asOfDate, tolerance);
            var valuationLineCount = CountValuationLines(companyConnectionString, asOfDate);
            var detailGlSum = accounts.Sum(a => a.Gl);
            var detailValSum = accounts.Sum(a => a.Valuation);
            var detailTotalsMatch = Math.Abs(detailGlSum - total.Gl) <= tolerance &&
                                    Math.Abs(detailValSum - total.Valuation) <= tolerance;

            var glAccountCount = accounts.Count(a => Math.Abs(a.Gl) > tolerance);
            var valuationAccountCount = accounts.Count(a => Math.Abs(a.Valuation) > tolerance);

            var difference = total.Valuation - total.Gl;
            var matches = total.Match && detailTotalsMatch;

            var sanityPassed = ValidateSanity(total.Gl, total.Valuation, valuationLineCount, detailTotalsMatch);

            var mainVariance = accounts
                .OrderByDescending(a => Math.Abs(a.Difference))
                .FirstOrDefault(a => Math.Abs(a.Difference) > tolerance);

            var drilldown = sanityPassed && !matches
                ? TryStockGroupDrilldown(companyConnectionString, asOfDate)
                : new List<StockGroupValuationRow>();

            var finding = !sanityPassed
                ? "Reconciliation result failed sanity validation; valuation side appears incomplete."
                : matches
                    ? "Inventory valuation is matching Balance Sheet stock value (within tolerance)."
                    : "Inventory valuation does not match Balance Sheet stock value.";

            return JsonSerializer.Serialize(new
            {
                querySerial = QuerySerial,
                asOfDate = asOfDate.ToString("yyyy-MM-dd"),
                balanceSheetStockValue = total.Gl,
                inventoryValuation = total.Valuation,
                difference,
                matches = sanityPassed && matches,
                reliableResult = sanityPassed,
                sanityCheckPassed = sanityPassed,
                executedSqlValuation = true,
                usedSdkFallback = false,
                valuationLineCount,
                valuationAccountCount,
                glAccountCount,
                detailTotalsMatchGrandTotal = detailTotalsMatch,
                toleranceAmount = tolerance,
                finding,
                mainVariance = mainVariance is null ? null : new
                {
                    glAccount = CleanAccountCode(mainVariance.Account),
                    glAccountName = mainVariance.GlAccountName,
                    balanceSheet = mainVariance.Gl,
                    valuation = mainVariance.Valuation,
                    difference = mainVariance.Difference
                },
                accounts = accounts.Select(a => new
                {
                    rowType = "DETAIL",
                    glAccount = CleanAccountCode(a.Account),
                    glAccountName = a.GlAccountName,
                    balanceSheet = a.Gl,
                    inventoryValuation = a.Valuation,
                    difference = a.Difference,
                    match = Math.Abs(a.Difference) <= tolerance ? "Yes" : "No"
                }),
                stockGroupValuation = drilldown.Take(15).Select(g => new
                {
                    stockGroup = g.StGroup,
                    stockGroupName = g.StockGroupName,
                    glAccount = CleanAccountCode(g.Account),
                    inventoryValuation = g.Valuation
                }),
                note = "Balance Sheet: PostGL on distinct inventory GL accounts (GrpTbl.StockAccLink, once per account). " +
                       "Valuation: Sage Inventory Valuation logic (_evInvCostTracking + _efnLastCostByDatePerItem). SDK fallback prohibited.",
                dataAsOfUtc = DateTimeOffset.UtcNow,
                executedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "I could not complete the reconciliation because the Sage SQL valuation query failed. " +
                "I will not use SDK item valuation as a substitute. " +
                $"Error: {ex.Message}",
                ex);
        }
    }

    private static (ReconcileTotal total, List<AccountReconcileRow> accounts) RunCanonicalQuery(
        string connectionString, DateTime asOfDate, decimal tolerance)
    {
        var accounts = new List<AccountReconcileRow>();
        ReconcileTotal? total = null;

        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(CanonicalSql, conn) { CommandTimeout = 180 };
        cmd.Parameters.AddWithValue("@pAsOfDate", asOfDate);
        cmd.Parameters.AddWithValue("@pTolerance", tolerance);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var rowType = (reader["RowType"]?.ToString() ?? "").Trim();
            var gl = ReadDecimal(reader, "BalanceSheetValue");
            var val = ReadDecimal(reader, "InventoryValuationValue");
            var diff = ReadDecimal(reader, "Difference");
            var matched = string.Equals(reader["IsMatched"]?.ToString(), "Yes", StringComparison.OrdinalIgnoreCase);

            if (rowType.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
            {
                total = new ReconcileTotal(gl, val, diff, matched);
                continue;
            }

            accounts.Add(new AccountReconcileRow(
                reader["Account"]?.ToString() ?? "",
                reader["GLAccountName"]?.ToString() ?? "",
                gl, val, diff));
        }

        if (total is null)
            throw new InvalidOperationException("Canonical query did not return a TOTAL row.");

        return (total, accounts);
    }

    private static List<StockGroupValuationRow> TryStockGroupDrilldown(string connectionString, DateTime asOfDate)
    {
        const string sql = """
            DECLARE @AsOfDate DATE = @pAsOfDate;
            DECLARE @NextDate DATE = DATEADD(DAY, 1, @AsOfDate);
            WITH LatestCT AS (
                SELECT CT.iStockID AS LastStockID, CT.iWarehouseID AS LastWarehouseID, MAX(CT.dTxDate) AS LastTxDate
                FROM _etblInvCostTracking CT WHERE CT.dTxDate < @NextDate
                GROUP BY CT.iStockID, CT.iWarehouseID
            ),
            FutureTrans AS (
                SELECT AccountLink AS StockLink, ISNULL(WarehouseID, 0) AS WarehouseID,
                    SUM(ISNULL(TransQtyOut, 0)) AS FutureQtyOut, SUM(ISNULL(TransQtyIn, 0)) AS FutureQtyIn
                FROM _bvSTTransactionsFull WHERE TxDate > @AsOfDate
                GROUP BY AccountLink, ISNULL(WarehouseID, 0)
            ),
            ValuationLines AS (
                SELECT G.StGroup, G.Description AS StockGroupName, A.Account,
                    ST.QtyInStock + CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyOut, 0) ELSE 0 END
                        - CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyIn, 0) ELSE 0 END AS QtyBalance,
                    ISNULL(CTE.ItemCostValue, 0) AS UnitCost
                FROM _evInvCostTracking ST
                INNER JOIN LatestCT CT ON ST.StockLink = CT.LastStockID AND ST.iWarehouseID = CT.LastWarehouseID AND ST.dTxDate = CT.LastTxDate
                LEFT JOIN FutureTrans FT ON FT.StockLink = ST.StockLink AND FT.WarehouseID = ISNULL(ST.iWarehouseID, 0)
                CROSS APPLY dbo._efnLastCostByDatePerItem(CT.LastStockID, ST.CostingMethod, ST.WhseItem, @NextDate) CTE
                INNER JOIN _etblStockDetails SD ON SD.idStockDetails = ST.idStockDetails
                INNER JOIN GrpTbl G ON SD.GroupID = G.idGrpTbl
                INNER JOIN Accounts A ON G.StockAccLink = A.AccountLink
                WHERE ST.ServiceItem <> 1 AND (ST.WhseItem = 0 OR (ST.WhseItem = 1 AND ST.iWarehouseID > 0))
            )
            SELECT StGroup, StockGroupName, Account,
                SUM(ISNULL(QtyBalance, 0) * ISNULL(UnitCost, 0)) AS ValuationValue
            FROM ValuationLines WHERE ISNULL(QtyBalance, 0) <> 0
            GROUP BY StGroup, StockGroupName, Account
            ORDER BY ABS(SUM(ISNULL(QtyBalance, 0) * ISNULL(UnitCost, 0))) DESC
            """;

        var list = new List<StockGroupValuationRow>();
        try
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
            cmd.Parameters.AddWithValue("@pAsOfDate", asOfDate);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new StockGroupValuationRow(
                    reader["StGroup"]?.ToString() ?? "",
                    reader["StockGroupName"]?.ToString() ?? "",
                    reader["Account"]?.ToString() ?? "",
                    ReadDecimal(reader, "ValuationValue")));
            }
        }
        catch
        {
            // drilldown optional
        }

        return list;
    }

    private static bool ValidateSanity(decimal gl, decimal valuation, int valuationLineCount, bool detailTotalsMatch)
    {
        if (!detailTotalsMatch)
            return false;
        if (valuation == 0 && gl != 0)
            return false;
        if (valuationLineCount <= 0 && gl != 0)
            return false;
        if (gl > 1_000_000m && gl != 0)
        {
            var ratio = Math.Abs(valuation / gl);
            if (ratio < 0.25m)
                return false;
        }

        return true;
    }

    private static int CountValuationLines(string connectionString, DateTime asOfDate)
    {
        const string sql = """
            DECLARE @AsOfDate DATE = @pAsOfDate;
            DECLARE @NextDate DATE = DATEADD(DAY, 1, @AsOfDate);
            WITH LatestCT AS (
                SELECT CT.iStockID AS LastStockID, CT.iWarehouseID AS LastWarehouseID, MAX(CT.dTxDate) AS LastTxDate
                FROM _etblInvCostTracking CT
                INNER JOIN StkItem SI ON CT.iStockID = SI.StockLink
                WHERE CT.dTxDate < @NextDate
                GROUP BY CT.iStockID, CT.iWarehouseID
            ),
            FutureTrans AS (
                SELECT AccountLink AS StockLink, ISNULL(WarehouseID, 0) AS WarehouseID,
                    SUM(ISNULL(TransQtyOut, 0)) AS FutureQtyOut, SUM(ISNULL(TransQtyIn, 0)) AS FutureQtyIn
                FROM _bvSTTransactionsFull WHERE TxDate > @AsOfDate
                GROUP BY AccountLink, ISNULL(WarehouseID, 0)
            ),
            ValuationLines AS (
                SELECT ST.StockLink
                FROM _evInvCostTracking ST
                INNER JOIN LatestCT CT ON ST.StockLink = CT.LastStockID AND ST.iWarehouseID = CT.LastWarehouseID AND ST.dTxDate = CT.LastTxDate
                LEFT JOIN FutureTrans FT ON FT.StockLink = ST.StockLink AND FT.WarehouseID = ISNULL(ST.iWarehouseID, 0)
                CROSS APPLY dbo._efnLastCostByDatePerItem(CT.LastStockID, ST.CostingMethod, ST.WhseItem, @NextDate) CTE
                INNER JOIN _etblStockDetails SD ON SD.idStockDetails = ST.idStockDetails
                INNER JOIN GrpTbl G ON SD.GroupID = G.idGrpTbl
                WHERE ST.ServiceItem <> 1 AND (ST.WhseItem = 0 OR (ST.WhseItem = 1 AND ST.iWarehouseID > 0))
                  AND (
                    ST.QtyInStock + CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyOut, 0) ELSE 0 END
                        - CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyIn, 0) ELSE 0 END
                  ) <> 0
            )
            SELECT COUNT(*) FROM ValuationLines;
            """;

        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@pAsOfDate", asOfDate);
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static DateTime ParseAsOfDate(Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("asOfDate", out var raw) &&
            DateTime.TryParse(raw, out var dt))
            return dt.Date;
        return DateTime.Today;
    }

    private static decimal ReadDecimal(IDataRecord reader, string column)
    {
        var val = reader[column];
        return val is decimal d ? d : Convert.ToDecimal(val);
    }

    private static string CleanAccountCode(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return "";
        return account.Trim().TrimStart('>');
    }

    private sealed record ReconcileTotal(decimal Gl, decimal Valuation, decimal Difference, bool Match);

    private sealed record AccountReconcileRow(
        string Account, string GlAccountName, decimal Gl, decimal Valuation, decimal Difference);

    private sealed record StockGroupValuationRow(
        string StGroup, string StockGroupName, string Account, decimal Valuation);
}
