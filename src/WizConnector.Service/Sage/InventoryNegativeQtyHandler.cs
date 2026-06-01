using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-INV-NEG-QTY-001 — physical negative quantity on hand (not BS GL).</summary>
internal static class InventoryNegativeQtyHandler
{
    public const string QuerySerial = "SAGE-INV-NEG-QTY-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 50);
        var sql = """
            SELECT TOP (@pTop)
                Code AS ItemCode,
                Description_1 AS Description,
                iWarehouseID,
                WhseCode,
                QtyBalance,
                StockValue
            FROM ValuationLines
            WHERE QtyBalance < 0
            ORDER BY QtyBalance ASC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql,
            cmd => cmd.Parameters.AddWithValue("@pTop", top));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            items = InventorySlowMovingTopHandler.MapRanked(rows),
            note = "Physical negative stock quantity from Sage valuation lines — not Balance Sheet GL credit balances.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
