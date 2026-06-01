using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class BankUnpresentedChequesHandler
{
    public const string QuerySerial = "SAGE-BANK-CHQ-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 25);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT TOP (@pTop)
                CAST(PG.TxDate AS DATE) AS TxDate,
                A.Account AS AccountCode,
                PG.Reference,
                PG.Description,
                ISNULL(PG.Debit, 0) AS Debit
            FROM PostGL PG
            {GlSqlHelper.BankJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {GlSqlHelper.BankFilter}
              AND ISNULL(PG.Debit, 0) > 0
              AND (
                LOWER(ISNULL(PG.Description,'')) LIKE '%cheque%'
                OR LOWER(ISNULL(PG.Description,'')) LIKE '%check%'
                OR LOWER(ISNULL(PG.Reference,'')) LIKE '%chq%'
              )
            ORDER BY PG.TxDate DESC;
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            InvNumSqlHelper.AddDateParameters(cmd, from, to);
        });

        var total = rows.Sum(r => GlSqlHelper.ToDecimal(r, "Debit"));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            reconciliationType = "Unpresented cheques",
            subledgerTotal = total,
            cheques = rows.Select((r, i) => new
            {
                rank = i + 1,
                txDate = r["TxDate"]?.ToString(),
                account = r["AccountCode"]?.ToString(),
                reference = r["Reference"]?.ToString(),
                amount = GlSqlHelper.ToDecimal(r, "Debit")
            }),
            finding = $"Unpresented cheque debits in period: {total:N2} ({rows.Count} lines).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
