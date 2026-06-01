using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-JRNL-USERS-001 — top users by journal posting value.</summary>
internal static class GlJournalUsersTopHandler
{
    public const string QuerySerial = "SAGE-GL-JRNL-USERS-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 10);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = """
            SELECT TOP (@pTop)
                ISNULL(PG.UserName, '(unknown)') AS UserName,
                COUNT(DISTINCT PG.cAuditNumber) AS JournalBatches,
                SUM(ABS(ISNULL(PG.Debit, 0) + ISNULL(PG.Credit, 0))) AS TotalValue
            FROM PostGL PG
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND UPPER(ISNULL(PG.Id, '')) IN ('JL', 'JNL')
            GROUP BY ISNULL(PG.UserName, '(unknown)')
            ORDER BY SUM(ABS(ISNULL(PG.Debit, 0) + ISNULL(PG.Credit, 0))) DESC;
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
            users = GlExpenseTopHandler.MapRanked(rows, "TotalValue"),
            note = "Top users by manual journal posting value (PostGL UserName).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
