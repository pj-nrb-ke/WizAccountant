using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class InventoryAdjustmentTopHandler
{
    public const string QuerySerial = "SAGE-INV-ADJ-TOP-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 20);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = """
            SELECT TOP (@pTop)
                SI.Code AS ItemCode,
                SI.Description_1 AS Description,
                SUM(ABS(ISNULL(T.Debit, 0) - ISNULL(T.Credit, 0))) AS AdjustmentValue
            FROM PostST T
            INNER JOIN StkItem SI ON SI.StockLink = T.AccountLink
            WHERE CAST(T.TxDate AS DATE) >= @pDateFrom AND CAST(T.TxDate AS DATE) <= @pDateTo
              AND UPPER(ISNULL(T.Id, '')) IN ('IJR', 'IJR')
            GROUP BY SI.Code, SI.Description_1
            ORDER BY SUM(ABS(ISNULL(T.Debit, 0) - ISNULL(T.Credit, 0))) DESC;
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            InvNumSqlHelper.AddDateParameters(cmd, from, to);
        });

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            items = GlExpenseTopHandler.MapRanked(rows, "AdjustmentValue"),
            note = "Top stock adjustments by value (PostST IJr) — inventory GL audit.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
