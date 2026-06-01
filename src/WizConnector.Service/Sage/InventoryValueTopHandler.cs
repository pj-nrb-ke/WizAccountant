using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-INV-VALUE-TOP-001 — top stock items by inventory value.</summary>
internal static class InventoryValueTopHandler
{
    public const string QuerySerial = "SAGE-INV-VALUE-TOP-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var sql = """
            SELECT TOP (@pTop)
                Code AS ItemCode,
                Description_1 AS Description,
                QtyBalance,
                StockValue
            FROM ValuationLines
            WHERE StockValue > 0
            ORDER BY StockValue DESC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql,
            cmd => cmd.Parameters.AddWithValue("@pTop", top));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            items = InventorySlowMovingTopHandler.MapRanked(rows),
            note = "Top items by Sage valuation (canonical costing SQL).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
