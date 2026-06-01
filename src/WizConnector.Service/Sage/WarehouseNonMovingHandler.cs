using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-WH-NONMOVE-001 — warehouse items with no movement for N days.</summary>
internal static class WarehouseNonMovingHandler
{
    public const string QuerySerial = "SAGE-WH-NONMOVE-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var minDays = SageListHelpers.ParseIntParam(parameters, "minDaysNoMove", 365);
        var sql = "," + InventoryValuationSqlHelper.LastMovementCte + """
            SELECT TOP (@pTop)
                VL.WhseCode AS WarehouseCode,
                VL.Code AS ItemCode,
                VL.Description_1 AS Description,
                VL.QtyBalance,
                VL.StockValue,
                M.LastMoveDate
            FROM ValuationLines VL
            LEFT JOIN ItemLastMove M ON M.StockLink = VL.StockLink AND M.WarehouseID = ISNULL(VL.iWarehouseID, 0)
            WHERE ISNULL(VL.iWarehouseID, 0) > 0
              AND VL.QtyBalance <> 0
              AND (M.LastMoveDate IS NULL OR DATEDIFF(DAY, M.LastMoveDate, @AsOfDate) >= @pMinDays)
            ORDER BY M.LastMoveDate ASC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            cmd.Parameters.AddWithValue("@pMinDays", minDays);
        });

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            minDaysNoMove = minDays,
            items = InventorySlowMovingTopHandler.MapRanked(rows),
            note = "Non-moving stock by warehouse (valuation + last movement).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
