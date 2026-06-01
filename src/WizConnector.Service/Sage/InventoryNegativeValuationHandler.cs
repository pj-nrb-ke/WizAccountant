using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-INV-NEG-VAL-001 — items with negative stock valuation.</summary>
internal static class InventoryNegativeValuationHandler
{
    public const string QuerySerial = "SAGE-INV-NEG-VAL-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 50);
        var sql = """
            SELECT TOP (@pTop)
                Code AS ItemCode,
                Description_1 AS Description,
                QtyBalance,
                UnitCost,
                StockValue
            FROM ValuationLines
            WHERE StockValue < 0
            ORDER BY StockValue ASC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql,
            cmd => cmd.Parameters.AddWithValue("@pTop", top));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            items = InventorySlowMovingTopHandler.MapRanked(rows),
            note = "Negative inventory valuation from canonical costing SQL (QtyBalance × UnitCost).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
