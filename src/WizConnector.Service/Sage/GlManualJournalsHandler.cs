using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-JRNL-MANUAL-001 — manual journal lines (Id = JL) in period.</summary>
internal static class GlManualJournalsHandler
{
    public const string QuerySerial = "SAGE-GL-JRNL-MANUAL-001";

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
                PG.Reference,
                PG.Description,
                ISNULL(PG.Debit, 0) AS Debit,
                ISNULL(PG.Credit, 0) AS Credit
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND UPPER(ISNULL(PG.Id, '')) IN ('JL', 'JNL')
            ORDER BY PG.TxDate DESC, ABS(ISNULL(PG.Debit, 0) + ISNULL(PG.Credit, 0)) DESC;
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
                auditNumber = r.GetValueOrDefault("AuditNumber")?.ToString(),
                txDate = r.GetValueOrDefault("TxDate") is DateTime d ? d.ToString("yyyy-MM-dd") : r["TxDate"]?.ToString(),
                userName = r.GetValueOrDefault("UserName")?.ToString(),
                accountCode = r.GetValueOrDefault("AccountCode")?.ToString(),
                reference = r.GetValueOrDefault("Reference")?.ToString(),
                debit = GlSqlHelper.ToDecimal(r, "Debit"),
                credit = GlSqlHelper.ToDecimal(r, "Credit")
            }),
            note = "Manual journal postings (PostGL Id JL) — read-only audit listing.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
