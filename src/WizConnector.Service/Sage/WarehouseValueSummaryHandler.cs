using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-WH-VALUE-001 — stock value aggregated by warehouse.</summary>
internal static class WarehouseValueSummaryHandler
{
    public const string QuerySerial = "SAGE-WH-VALUE-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 20);
        var sql = """
            SELECT TOP (@pTop)
                ISNULL(WhseCode, '(No warehouse)') AS WarehouseCode,
                iWarehouseID AS WarehouseId,
                SUM(StockValue) AS TotalStockValue,
                SUM(QtyBalance) AS TotalQty
            FROM ValuationLines
            WHERE ISNULL(QtyBalance, 0) <> 0
            GROUP BY iWarehouseID, WhseCode
            ORDER BY SUM(StockValue) DESC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql,
            cmd => cmd.Parameters.AddWithValue("@pTop", top));

        var mapped = rows.Select((r, i) => new
        {
            rank = i + 1,
            warehouseCode = r.GetValueOrDefault("WarehouseCode")?.ToString(),
            warehouseId = r.GetValueOrDefault("WarehouseId"),
            totalStockValue = InventorySlowMovingTopHandler.ToDecimal(r, "TotalStockValue"),
            totalQty = InventorySlowMovingTopHandler.ToDecimal(r, "TotalQty")
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            warehouses = mapped,
            countOnly = false,
            note = "Stock value by warehouse from canonical valuation SQL.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
