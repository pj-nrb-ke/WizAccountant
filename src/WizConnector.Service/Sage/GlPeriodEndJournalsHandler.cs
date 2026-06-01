using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-JRNL-PERIODEND-001 — journals in last 5 days of month (period-end adjustments).</summary>
internal static class GlPeriodEndJournalsHandler
{
    public const string QuerySerial = "SAGE-GL-JRNL-PERIODEND-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 25);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = """
            SELECT TOP (@pTop)
                PG.cAuditNumber AS AuditNumber,
                CAST(PG.TxDate AS DATE) AS TxDate,
                PG.UserName,
                A.Account AS AccountCode,
                PG.Description,
                ISNULL(PG.Debit, 0) AS Debit,
                ISNULL(PG.Credit, 0) AS Credit
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND UPPER(ISNULL(PG.Id, '')) IN ('JL', 'JNL')
              AND DAY(PG.TxDate) >= DAY(EOMONTH(PG.TxDate)) - 4
            ORDER BY PG.TxDate DESC;
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
            journals = rows.Select((r, i) => new { rank = i + 1, auditNumber = r["AuditNumber"]?.ToString(), txDate = r["TxDate"]?.ToString(), userName = r["UserName"]?.ToString() }),
            note = "Journal lines dated in last 5 days of month (period-end adjustment proxy).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
