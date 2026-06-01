using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-EXP-TOP-001 — top expense GL accounts by net debit in period.</summary>
internal static class GlExpenseTopHandler
{
    public const string QuerySerial = "SAGE-GL-EXP-TOP-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 10);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT TOP (@pTop)
                A.Account AS AccountCode,
                A.Description AS AccountName,
                SUM({GlSqlHelper.NetValueExpr}) AS ExpenseValue
            FROM PostGL PG
            {GlSqlHelper.ExpenseJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {GlSqlHelper.ExpenseFilter}
            GROUP BY A.Account, A.Description
            HAVING SUM({GlSqlHelper.NetValueExpr}) > 0
            ORDER BY SUM({GlSqlHelper.NetValueExpr}) DESC;
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
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            accounts = MapRanked(rows, "ExpenseValue"),
            note = "Top expense accounts from PostGL net movement (expense-type GL accounts).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    internal static List<object> MapRanked(List<Dictionary<string, object?>> rows, string valueKey) =>
        rows.Select((r, i) => new
        {
            rank = i + 1,
            code = r.GetValueOrDefault("AccountCode")?.ToString(),
            name = r.GetValueOrDefault("AccountName")?.ToString(),
            value = GlSqlHelper.ToDecimal(r, valueKey)
        }).Cast<object>().ToList();
}
