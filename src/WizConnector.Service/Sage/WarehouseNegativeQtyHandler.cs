using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-WH-NEG-QTY-001 — warehouses with negative physical stock lines.</summary>
internal static class WarehouseNegativeQtyHandler
{
    public const string QuerySerial = "SAGE-WH-NEG-QTY-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var sql = """
            SELECT TOP (@pTop)
                WhseCode AS WarehouseCode,
                Code AS ItemCode,
                Description_1 AS Description,
                QtyBalance,
                StockValue
            FROM ValuationLines
            WHERE QtyBalance < 0 AND ISNULL(iWarehouseID, 0) > 0
            ORDER BY QtyBalance ASC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql,
            cmd => cmd.Parameters.AddWithValue("@pTop", top));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            items = InventorySlowMovingTopHandler.MapRanked(rows),
            note = "Negative stock quantity by warehouse — physical qty, not GL.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
