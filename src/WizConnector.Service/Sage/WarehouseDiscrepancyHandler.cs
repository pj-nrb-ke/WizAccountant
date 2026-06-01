using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-WH-DISCREPANCY-001 — warehouse discrepancy candidates (negative qty + adjustments).</summary>
internal static class WarehouseDiscrepancyHandler
{
    public const string QuerySerial = "SAGE-WH-DISCREPANCY-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 15);
        var sql = """
            , NegLines AS (
                SELECT WhseCode, COUNT(*) AS NegativeLineCount
                FROM ValuationLines
                WHERE QtyBalance < 0 AND ISNULL(iWarehouseID, 0) > 0
                GROUP BY WhseCode, iWarehouseID
            ),
            AdjLines AS (
                SELECT ISNULL(W.Code, CAST(T.WarehouseID AS VARCHAR(20))) AS WhseCode,
                    COUNT(*) AS AdjustmentCount
                FROM _bvSTTransactionsFull T
                LEFT JOIN WhseMst W ON W.WhseLink = T.WarehouseID
                WHERE CAST(T.TxDate AS DATE) >= DATEADD(DAY, -30, @AsOfDate)
                  AND (
                        LOWER(ISNULL(T.Description, '')) LIKE '%adjust%'
                        OR LOWER(ISNULL(T.Reference, '')) LIKE '%adjust%'
                        OR LOWER(ISNULL(T.Description, '')) LIKE '%variance%'
                      )
                GROUP BY ISNULL(W.Code, CAST(T.WarehouseID AS VARCHAR(20)))
            )
            SELECT TOP (@pTop)
                COALESCE(N.WhseCode, A.WhseCode) AS WarehouseCode,
                ISNULL(N.NegativeLineCount, 0) AS NegativeLineCount,
                ISNULL(A.AdjustmentCount, 0) AS RecentAdjustmentCount
            FROM NegLines N
            FULL OUTER JOIN AdjLines A ON N.WhseCode = A.WhseCode
            WHERE ISNULL(N.NegativeLineCount, 0) > 0 OR ISNULL(A.AdjustmentCount, 0) > 0
            ORDER BY ISNULL(N.NegativeLineCount, 0) + ISNULL(A.AdjustmentCount, 0) DESC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql,
            cmd => cmd.Parameters.AddWithValue("@pTop", top));

        var mapped = rows.Select((r, i) => new
        {
            rank = i + 1,
            warehouseCode = r.GetValueOrDefault("WarehouseCode")?.ToString(),
            negativeLineCount = Convert.ToInt32(r.GetValueOrDefault("NegativeLineCount") ?? 0),
            recentAdjustmentCount = Convert.ToInt32(r.GetValueOrDefault("RecentAdjustmentCount") ?? 0)
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            warehouses = mapped,
            note = "Warehouses with negative stock lines and/or recent adjustment transactions.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
