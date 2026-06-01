using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-EXP-TREND-001 — monthly expense trend from PostGL.</summary>
internal static class GlExpenseTrendHandler
{
    public const string QuerySerial = "SAGE-GL-EXP-TREND-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT
                YEAR(PG.TxDate) AS Yr,
                MONTH(PG.TxDate) AS Mo,
                SUM({GlSqlHelper.NetValueExpr}) AS ExpenseValue
            FROM PostGL PG
            {GlSqlHelper.ExpenseJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {GlSqlHelper.ExpenseFilter}
            GROUP BY YEAR(PG.TxDate), MONTH(PG.TxDate)
            ORDER BY YEAR(PG.TxDate), MONTH(PG.TxDate);
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql,
            cmd => InvNumSqlHelper.AddDateParameters(cmd, from, to));

        var months = rows.Select(r => new
        {
            period = $"{r["Yr"]}-{Convert.ToInt32(r["Mo"]):D2}",
            expenseValue = GlSqlHelper.ToDecimal(r, "ExpenseValue")
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            months,
            countOnly = false,
            note = "Monthly expense totals from PostGL (trend analysis).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
