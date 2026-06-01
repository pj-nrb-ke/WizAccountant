using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-BACKDATE-001 — journals where posting date (DTStamp) is after TxDate by 7+ days.</summary>
internal static class GlBackdatedTransactionsHandler
{
    public const string QuerySerial = "SAGE-GL-BACKDATE-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 25);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = """
            SELECT TOP (@pTop)
                PG.cAuditNumber AS AuditNumber,
                CAST(PG.TxDate AS DATE) AS TxDate,
                CAST(PG.DTStamp AS DATE) AS PostedDate,
                DATEDIFF(DAY, CAST(PG.TxDate AS DATE), CAST(PG.DTStamp AS DATE)) AS DaysLate,
                PG.UserName,
                A.Account AS AccountCode,
                ISNULL(PG.Debit, 0) + ISNULL(PG.Credit, 0) AS Amount
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND PG.DTStamp IS NOT NULL
              AND DATEDIFF(DAY, CAST(PG.TxDate AS DATE), CAST(PG.DTStamp AS DATE)) >= 7
            ORDER BY DATEDIFF(DAY, CAST(PG.TxDate AS DATE), CAST(PG.DTStamp AS DATE)) DESC;
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
            transactions = rows.Select((r, i) => new
            {
                rank = i + 1,
                auditNumber = r.GetValueOrDefault("AuditNumber")?.ToString(),
                txDate = r["TxDate"]?.ToString(),
                postedDate = r["PostedDate"]?.ToString(),
                daysLate = Convert.ToInt32(r.GetValueOrDefault("DaysLate") ?? 0),
                userName = r.GetValueOrDefault("UserName")?.ToString(),
                accountCode = r.GetValueOrDefault("AccountCode")?.ToString(),
                amount = GlSqlHelper.ToDecimal(r, "Amount")
            }),
            note = "Backdated/late-posted GL lines (DTStamp vs TxDate) — audit only.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
