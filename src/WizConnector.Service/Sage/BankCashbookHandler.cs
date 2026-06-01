using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class BankCashbookHandler
{
    public const string QuerySerial = "SAGE-BANK-CB-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 25);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT TOP (@pTop)
                CAST(PG.TxDate AS DATE) AS TxDate,
                A.Account AS AccountCode,
                A.Description AS AccountName,
                PG.Reference,
                PG.Description,
                ISNULL(PG.Debit, 0) AS Debit,
                ISNULL(PG.Credit, 0) AS Credit
            FROM PostGL PG
            {GlSqlHelper.BankJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {GlSqlHelper.BankFilter}
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
            transactions = rows.Select((r, i) => new
            {
                rank = i + 1,
                txDate = r["TxDate"]?.ToString(),
                accountCode = r["AccountCode"]?.ToString(),
                debit = GlSqlHelper.ToDecimal(r, "Debit"),
                credit = GlSqlHelper.ToDecimal(r, "Credit")
            }),
            note = "Bank/cashbook GL postings (bank-type accounts on PostGL).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
