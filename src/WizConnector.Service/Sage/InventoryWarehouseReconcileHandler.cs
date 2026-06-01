using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class InventoryWarehouseReconcileHandler
{
    public const string QuerySerial = "SAGE-INV-WH-RECON-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 15);
        var asOf = ReconcileSqlHelper.ParseAsOf(parameters);
        parameters["asOfDate"] = asOf.ToString("yyyy-MM-dd");

        var reconJson = InventoryGlReconcileHandler.Execute(companyConnectionString, parameters);
        using var doc = JsonDocument.Parse(reconJson);
        var root = doc.RootElement;
        var subledger = root.TryGetProperty("inventoryValuation", out var v) ? v.GetDecimal() : 0m;
        var glTotal = root.TryGetProperty("balanceSheetStockValue", out var g) ? g.GetDecimal() : 0m;

        var sql = """
            SELECT TOP (@pTop)
                ISNULL(WhseCode, '(No warehouse)') AS WarehouseCode,
                SUM(StockValue) AS WarehouseValuation
            FROM ValuationLines
            WHERE ISNULL(QtyBalance, 0) <> 0
            GROUP BY iWarehouseID, WhseCode
            ORDER BY ABS(SUM(StockValue)) DESC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            cmd.Parameters["@pAsOfDate"].Value = asOf;
        });

        var totalWh = rows.Sum(r => InventorySlowMovingTopHandler.ToDecimal(r, "WarehouseValuation"));
        var topList = rows.Select((r, i) =>
        {
            var whVal = InventorySlowMovingTopHandler.ToDecimal(r, "WarehouseValuation");
            return (object)new
            {
                rank = i + 1,
                warehouse = r.GetValueOrDefault("WarehouseCode")?.ToString(),
                valuation = whVal,
                sharePercent = totalWh > 0 ? Math.Round(whVal / totalWh * 100, 2) : 0m
            };
        }).ToList();

        return ReconcileEnvelope.Build(
            QuerySerial,
            "Inventory by warehouse (valuation contributors)",
            subledger,
            glTotal,
            topList,
            "Warehouse breakdown of inventory valuation vs overall GL reconciliation totals.",
            root.TryGetProperty("matches", out var m) && m.ValueKind == JsonValueKind.True,
            new { asOfDate = asOf.ToString("yyyy-MM-dd"), parentQuery = InventoryGlReconcileHandler.QuerySerial });
    }
}
