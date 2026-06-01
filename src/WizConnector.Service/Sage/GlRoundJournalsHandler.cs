using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-JRNL-ROUND-001 — suspicious round-value journal amounts.</summary>
internal static class GlRoundJournalsHandler
{
    public const string QuerySerial = "SAGE-GL-JRNL-ROUND-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 20);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = """
            SELECT TOP (@pTop)
                PG.cAuditNumber AS AuditNumber,
                CAST(PG.TxDate AS DATE) AS TxDate,
                PG.UserName,
                A.Account AS AccountCode,
                ISNULL(PG.Debit, 0) AS Debit,
                ISNULL(PG.Credit, 0) AS Credit
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND UPPER(ISNULL(PG.Id, '')) IN ('JL', 'JNL')
              AND (
                    (ISNULL(PG.Debit, 0) >= 1000 AND ISNULL(PG.Debit, 0) % 1000 = 0)
                    OR (ISNULL(PG.Credit, 0) >= 1000 AND ISNULL(PG.Credit, 0) % 1000 = 0)
                  )
            ORDER BY ABS(ISNULL(PG.Debit, 0) + ISNULL(PG.Credit, 0)) DESC;
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
            journals = rows.Select((r, i) => new
            {
                rank = i + 1,
                auditNumber = r["AuditNumber"]?.ToString(),
                txDate = r["TxDate"]?.ToString(),
                userName = r["UserName"]?.ToString(),
                accountCode = r["AccountCode"]?.ToString(),
                debit = GlSqlHelper.ToDecimal(r, "Debit"),
                credit = GlSqlHelper.ToDecimal(r, "Credit")
            }),
            note = "Round-thousand journal amounts (fraud/audit indicator).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
