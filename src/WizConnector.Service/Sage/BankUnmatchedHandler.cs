using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class BankUnmatchedHandler
{
    public const string QuerySerial = "SAGE-BANK-UNMATCH-001";

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
                ISNULL(PG.Debit, 0) AS Debit,
                ISNULL(PG.Credit, 0) AS Credit,
                {GlSqlHelper.NetValueExpr} AS NetAmount
            FROM PostGL PG
            {GlSqlHelper.BankJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {GlSqlHelper.BankFilter}
              AND (
                LOWER(ISNULL(PG.Description,'')) LIKE '%unalloc%'
                OR LOWER(ISNULL(PG.Description,'')) LIKE '%unmatched%'
                OR LOWER(ISNULL(PG.Reference,'')) LIKE '%unalloc%'
              )
            ORDER BY ABS({GlSqlHelper.NetValueExpr}) DESC;
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            InvNumSqlHelper.AddDateParameters(cmd, from, to);
        });

        var total = rows.Sum(r => Math.Abs(GlSqlHelper.ToDecimal(r, "NetAmount")));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            reconciliationType = "Unmatched bank entries",
            subledgerTotal = total,
            entries = rows.Select((r, i) => new
            {
                rank = i + 1,
                txDate = r["TxDate"]?.ToString(),
                account = r["AccountCode"]?.ToString(),
                reference = r["Reference"]?.ToString(),
                netAmount = GlSqlHelper.ToDecimal(r, "NetAmount")
            }),
            finding = $"Unmatched bank GL lines in period: {rows.Count} entries, {total:N2} absolute total.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
