using System.Data.SqlClient;

namespace WizConnector.Service.Sage;

/// <summary>
/// Canonical Sage inventory valuation CTE (SAGE-INVVAL-RECON-CANONICAL-001 pattern).
/// Do not use naive PostST sum for valuation analytics.
/// </summary>
internal static class InventoryValuationSqlHelper
{
    public const string ValuationCtePrefix = """
        DECLARE @AsOfDate DATE = @pAsOfDate;
        DECLARE @NextDate DATE = DATEADD(DAY, 1, @AsOfDate);

        WITH LatestCT AS
        (
            SELECT CT.iStockID AS LastStockID, CT.iWarehouseID AS LastWarehouseID, MAX(CT.dTxDate) AS LastTxDate
            FROM _etblInvCostTracking CT
            INNER JOIN StkItem SI ON CT.iStockID = SI.StockLink
            WHERE CT.dTxDate < @NextDate
            GROUP BY CT.iStockID, CT.iWarehouseID
        ),
        FutureTrans AS
        (
            SELECT AccountLink AS StockLink, ISNULL(WarehouseID, 0) AS WarehouseID,
                SUM(ISNULL(TransQtyOut, 0)) AS FutureQtyOut,
                SUM(ISNULL(TransQtyIn, 0)) AS FutureQtyIn
            FROM _bvSTTransactionsFull
            WHERE TxDate > @AsOfDate
            GROUP BY AccountLink, ISNULL(WarehouseID, 0)
        ),
        ValuationLines AS
        (
            SELECT
                ST.StockLink,
                ST.Code,
                ST.Description_1,
                ST.iWarehouseID,
                ST.WhseCode,
                ST.idStockDetails,
                ST.ServiceItem,
                QtyBalance =
                    ST.QtyInStock
                    + CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyOut, 0) ELSE 0 END
                    - CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyIn, 0) ELSE 0 END,
                UnitCost = ISNULL(CTE.ItemCostValue, 0),
                StockValue = (
                    ST.QtyInStock
                    + CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyOut, 0) ELSE 0 END
                    - CASE WHEN ST.ServiceItem = 0 THEN ISNULL(FT.FutureQtyIn, 0) ELSE 0 END
                ) * ISNULL(CTE.ItemCostValue, 0)
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
            WHERE ST.ServiceItem <> 1
              AND (ST.WhseItem = 0 OR (ST.WhseItem = 1 AND ST.iWarehouseID > 0))
        )
        """;

    public static string LastMovementCte => """
        ItemLastMove AS
        (
            SELECT AccountLink AS StockLink, ISNULL(WarehouseID, 0) AS WarehouseID,
                MAX(CAST(TxDate AS DATE)) AS LastMoveDate
            FROM _bvSTTransactionsFull
            WHERE CAST(TxDate AS DATE) <= @AsOfDate
            GROUP BY AccountLink, ISNULL(WarehouseID, 0)
        )
        """;

    public static List<Dictionary<string, object?>> ExecuteRows(
        string connectionString,
        string sqlAfterCte,
        Action<SqlCommand>? configure = null)
    {
        var fullSql = ValuationCtePrefix + sqlAfterCte;
        var rows = new List<Dictionary<string, object?>>();
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(fullSql, conn) { CommandTimeout = 180 };
        cmd.Parameters.AddWithValue("@pAsOfDate", DateTime.Today);
        configure?.Invoke(cmd);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(dict);
        }

        return rows;
    }

    public static int ExecuteScalarCount(string connectionString, string sqlAfterCte)
    {
        var fullSql = ValuationCtePrefix + sqlAfterCte;
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(fullSql, conn) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@pAsOfDate", DateTime.Today);
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }
}
