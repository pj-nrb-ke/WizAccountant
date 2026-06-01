using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>Follow-up drilldown for a stock item after reconciliation (SAGE-TRAIN-006 Scope H).</summary>
internal static class InventoryItemDrilldownHandler
{
    public const string QuerySerial = "SAGE-INV-ITEM-DRILL-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var code = parameters.GetValueOrDefault("stockCode") ?? parameters.GetValueOrDefault("itemCode") ?? "";
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Stock item code is required for drilldown (e.g. NEWRM01).");

        var asOf = ReconcileSqlHelper.ParseAsOf(parameters);
        var sql = """
            SELECT TOP (1)
                ST.Code,
                ST.Description_1 AS Description,
                ST.iWarehouseID,
                ST.WhseCode,
                ST.QtyInStock,
                ISNULL(CTE.ItemCostValue, 0) AS UnitCost,
                (ST.QtyInStock * ISNULL(CTE.ItemCostValue, 0)) AS LineValuation
            FROM _evInvCostTracking ST
            CROSS APPLY dbo._efnLastCostByDatePerItem(
                ST.StockLink, ST.CostingMethod, ST.WhseItem, @pNextDate) CTE
            WHERE ST.Code = @pCode
            ORDER BY ST.iWarehouseID;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pCode", code.Trim());
            cmd.Parameters.AddWithValue("@pNextDate", asOf.AddDays(1));
        });

        if (rows.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                querySerial = QuerySerial,
                stockCode = code,
                finding = $"No valuation row found for item {code} as at {asOf:yyyy-MM-dd}.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
        }

        var r = rows[0];
        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            stockCode = r.GetValueOrDefault("Code")?.ToString(),
            description = r.GetValueOrDefault("Description")?.ToString(),
            warehouse = r.GetValueOrDefault("WhseCode")?.ToString(),
            qtyOnHand = InventorySlowMovingTopHandler.ToDecimal(r, "QtyInStock"),
            unitCost = InventorySlowMovingTopHandler.ToDecimal(r, "UnitCost"),
            lineValuation = InventorySlowMovingTopHandler.ToDecimal(r, "LineValuation"),
            asOfDate = asOf.ToString("yyyy-MM-dd"),
            finding = $"Item {code} valuation detail as at {asOf:yyyy-MM-dd}.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
