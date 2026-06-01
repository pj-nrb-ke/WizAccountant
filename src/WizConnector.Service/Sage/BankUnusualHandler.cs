using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class BankUnusualHandler
{
    public const string QuerySerial = "SAGE-BANK-UNUSUAL-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 20);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT TOP (@pTop)
                CAST(PG.TxDate AS DATE) AS TxDate,
                A.Account AS AccountCode,
                PG.Reference,
                ISNULL(PG.Debit, 0) + ISNULL(PG.Credit, 0) AS Amount
            FROM PostGL PG
            {GlSqlHelper.BankJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {GlSqlHelper.BankFilter}
              AND (ISNULL(PG.Debit, 0) + ISNULL(PG.Credit, 0)) >= 50000
            ORDER BY (ISNULL(PG.Debit, 0) + ISNULL(PG.Credit, 0)) DESC;
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
            transactions = rows.Select((r, i) => new { rank = i + 1, accountCode = r["AccountCode"]?.ToString(), amount = GlSqlHelper.ToDecimal(r, "Amount") }),
            note = "Large/unusual bank GL amounts — audit indicator.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
