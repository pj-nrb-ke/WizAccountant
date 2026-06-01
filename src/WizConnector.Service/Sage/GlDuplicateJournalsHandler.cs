using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-JRNL-DUP-001 — duplicate journal batches (same audit, account, amount, date).</summary>
internal static class GlDuplicateJournalsHandler
{
    public const string QuerySerial = "SAGE-GL-JRNL-DUP-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 20);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = """
            SELECT TOP (@pTop)
                PG.cAuditNumber AS AuditNumber,
                CAST(PG.TxDate AS DATE) AS TxDate,
                A.Account AS AccountCode,
                ISNULL(PG.Debit, 0) AS Debit,
                ISNULL(PG.Credit, 0) AS Credit,
                COUNT(*) AS LineCount
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND UPPER(ISNULL(PG.Id, '')) IN ('JL', 'JNL')
            GROUP BY PG.cAuditNumber, CAST(PG.TxDate AS DATE), A.Account, ISNULL(PG.Debit, 0), ISNULL(PG.Credit, 0)
            HAVING COUNT(*) > 1
            ORDER BY COUNT(*) DESC;
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
            duplicates = rows.Select((r, i) => new { rank = i + 1, auditNumber = r["AuditNumber"]?.ToString(), lineCount = Convert.ToInt32(r["LineCount"] ?? 0) }),
            note = "Potential duplicate journal line groups — audit only.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
