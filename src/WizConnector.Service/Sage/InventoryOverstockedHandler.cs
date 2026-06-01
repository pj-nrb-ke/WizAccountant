using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-INV-OVERSTOCK-001 — overstocked items (high qty vs reorder, slow movement).</summary>
internal static class InventoryOverstockedHandler
{
    public const string QuerySerial = "SAGE-INV-OVERSTOCK-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var sql = "," + InventoryValuationSqlHelper.LastMovementCte + """
            SELECT TOP (@pTop)
                VL.Code AS ItemCode,
                VL.Description_1 AS Description,
                VL.QtyBalance,
                VL.StockValue,
                ISNULL(SD.fReorderQty, ISNULL(SI.ReOrder, 0)) AS ReorderLevel,
                M.LastMoveDate
            FROM ValuationLines VL
            INNER JOIN StkItem SI ON SI.StockLink = VL.StockLink
            LEFT JOIN _etblStockDetails SD ON SD.idStockDetails = VL.idStockDetails
            LEFT JOIN ItemLastMove M ON M.StockLink = VL.StockLink AND M.WarehouseID = ISNULL(VL.iWarehouseID, 0)
            WHERE VL.QtyBalance > 0
              AND ISNULL(SD.fReorderQty, ISNULL(SI.ReOrder, 0)) > 0
              AND VL.QtyBalance > ISNULL(SD.fReorderQty, ISNULL(SI.ReOrder, 0)) * 3
              AND (M.LastMoveDate IS NULL OR DATEDIFF(DAY, M.LastMoveDate, @AsOfDate) > 90)
            ORDER BY VL.StockValue DESC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql,
            cmd => cmd.Parameters.AddWithValue("@pTop", top));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            items = InventorySlowMovingTopHandler.MapRanked(rows),
            note = "Overstocked: qty > 3× reorder and no movement in 90+ days.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
