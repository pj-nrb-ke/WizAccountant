using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-INV-SLOW-001 — slow moving stock (oldest last movement, qty on hand).</summary>
internal static class InventorySlowMovingTopHandler
{
    public const string QuerySerial = "SAGE-INV-SLOW-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 20);
        var sql = "," + InventoryValuationSqlHelper.LastMovementCte + """
            SELECT TOP (@pTop)
                VL.Code AS ItemCode,
                VL.Description_1 AS Description,
                VL.QtyBalance,
                VL.StockValue,
                M.LastMoveDate,
                DATEDIFF(DAY, M.LastMoveDate, @AsOfDate) AS DaysSinceMove
            FROM ValuationLines VL
            INNER JOIN ItemLastMove M ON M.StockLink = VL.StockLink AND M.WarehouseID = ISNULL(VL.iWarehouseID, 0)
            WHERE VL.QtyBalance > 0 AND M.LastMoveDate IS NOT NULL
            ORDER BY M.LastMoveDate ASC, VL.StockValue DESC;
            """;

        var rows = InventoryValuationSqlHelper.ExecuteRows(companyConnectionString, sql, cmd =>
            cmd.Parameters.AddWithValue("@pTop", top));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            items = MapRanked(rows),
            note = "Slow moving: stock on hand with oldest last movement date (canonical valuation SQL).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    internal static List<object> MapRanked(List<Dictionary<string, object?>> rows) =>
        rows.Select((r, i) => new
        {
            rank = i + 1,
            code = r.GetValueOrDefault("ItemCode")?.ToString(),
            description = r.GetValueOrDefault("Description")?.ToString(),
            qtyOnHand = ToDecimal(r, "QtyBalance"),
            stockValue = ToDecimal(r, "StockValue"),
            lastMoveDate = r.GetValueOrDefault("LastMoveDate") is DateTime d ? d.ToString("yyyy-MM-dd") : r.GetValueOrDefault("LastMoveDate")?.ToString(),
            daysSinceMove = r.GetValueOrDefault("DaysSinceMove") is int di ? di : Convert.ToInt32(r.GetValueOrDefault("DaysSinceMove") ?? 0)
        }).Cast<object>().ToList();

    internal static decimal ToDecimal(Dictionary<string, object?> r, string key) =>
        r.TryGetValue(key, out var v) && v is not null ? Convert.ToDecimal(v) : 0m;
}
