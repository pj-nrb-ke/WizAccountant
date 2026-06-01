using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-EXP-VAR-001 — expense accounts with significant month-over-month change.</summary>
internal static class GlExpenseVarianceHandler
{
    public const string QuerySerial = "SAGE-GL-EXP-VAR-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 15);
        var today = DateTime.Today;
        var curStart = new DateTime(today.Year, today.Month, 1);
        var curEnd = today;
        var priorStart = curStart.AddMonths(-1);
        var priorEnd = curStart.AddDays(-1);

        var sql = $"""
            WITH Curr AS (
                SELECT A.Account, A.Description, SUM({GlSqlHelper.NetValueExpr}) AS Val
                FROM PostGL PG {GlSqlHelper.ExpenseJoin}
                WHERE CAST(PG.TxDate AS DATE) BETWEEN @pCurFrom AND @pCurTo AND {GlSqlHelper.ExpenseFilter}
                GROUP BY A.Account, A.Description
            ),
            Prior AS (
                SELECT A.Account, SUM({GlSqlHelper.NetValueExpr}) AS Val
                FROM PostGL PG {GlSqlHelper.ExpenseJoin}
                WHERE CAST(PG.TxDate AS DATE) BETWEEN @pPriorFrom AND @pPriorTo AND {GlSqlHelper.ExpenseFilter}
                GROUP BY A.Account
            )
            SELECT TOP (@pTop)
                C.Account AS AccountCode,
                C.Description AS AccountName,
                ISNULL(C.Val, 0) AS CurrentValue,
                ISNULL(P.Val, 0) AS PriorValue,
                ISNULL(C.Val, 0) - ISNULL(P.Val, 0) AS Variance
            FROM Curr C
            LEFT JOIN Prior P ON P.Account = C.Account
            WHERE ABS(ISNULL(C.Val, 0) - ISNULL(P.Val, 0)) > 0.01
            ORDER BY ABS(ISNULL(C.Val, 0) - ISNULL(P.Val, 0)) DESC;
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            cmd.Parameters.AddWithValue("@pCurFrom", curStart);
            cmd.Parameters.AddWithValue("@pCurTo", curEnd);
            cmd.Parameters.AddWithValue("@pPriorFrom", priorStart);
            cmd.Parameters.AddWithValue("@pPriorTo", priorEnd);
        });

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            currentPeriod = $"{curStart:yyyy-MM-dd} to {curEnd:yyyy-MM-dd}",
            priorPeriod = $"{priorStart:yyyy-MM-dd} to {priorEnd:yyyy-MM-dd}",
            accounts = rows.Select((r, i) => new
            {
                rank = i + 1,
                code = r.GetValueOrDefault("AccountCode")?.ToString(),
                name = r.GetValueOrDefault("AccountName")?.ToString(),
                currentValue = GlSqlHelper.ToDecimal(r, "CurrentValue"),
                priorValue = GlSqlHelper.ToDecimal(r, "PriorValue"),
                variance = GlSqlHelper.ToDecimal(r, "Variance")
            }),
            note = "Expense accounts with largest absolute variance vs prior month.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
