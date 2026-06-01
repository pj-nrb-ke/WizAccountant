using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-BAL-UNUSUAL-001 — GL accounts with large absolute net balance.</summary>
internal static class GlUnusualBalancesHandler
{
    public const string QuerySerial = "SAGE-GL-BAL-UNUSUAL-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 15);
        var asOf = DateTime.Today;
        var sql = $"""
            SELECT TOP (@pTop)
                A.Account AS AccountCode,
                A.Description AS AccountName,
                SUM({GlSqlHelper.NetValueExpr}) AS NetBalance
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            WHERE CAST(PG.TxDate AS DATE) <= @pAsOf
            GROUP BY A.Account, A.Description
            HAVING ABS(SUM({GlSqlHelper.NetValueExpr})) > 0.01
            ORDER BY ABS(SUM({GlSqlHelper.NetValueExpr})) DESC;
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            cmd.Parameters.AddWithValue("@pAsOf", asOf);
        });

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            asOfDate = asOf.ToString("yyyy-MM-dd"),
            accounts = GlExpenseTopHandler.MapRanked(rows, "NetBalance"),
            note = "GL accounts ranked by absolute net balance (unusual size indicator).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
