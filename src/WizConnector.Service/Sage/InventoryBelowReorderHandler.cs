using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-INV-REORDER-001 — items below reorder level.</summary>
internal static class InventoryBelowReorderHandler
{
    public const string QuerySerial = "SAGE-INV-REORDER-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 50);
        var sql = """
            SELECT TOP (@pTop)
                VL.Code AS ItemCode,
                VL.Description_1 AS Description,
                VL.QtyBalance,
                ISNULL(SD.fReorderQty, ISNULL(SI.ReOrder, 0)) AS ReorderLevel
            FROM ValuationLines VL
            INNER JOIN StkItem SI ON SI.StockLink = VL.StockLink
            LEFT JOIN _etblStockDetails SD ON SD.idStockDetails = VL.idStockDetails
            WHERE ISNULL(SD.fReorderQty, ISNULL(SI.ReOrder, 0)) > 0
              AND VL.QtyBalance < ISNULL(SD.fReorderQty, ISNULL(SI.ReOrder, 0))
            ORDER BY VL.QtyBalance ASC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql,
            cmd => cmd.Parameters.AddWithValue("@pTop", top));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            items = InventorySlowMovingTopHandler.MapRanked(rows),
            note = "Items where quantity on hand is below reorder/minimum level.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
