using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class BankDailyCashHandler
{
    public const string QuerySerial = "SAGE-BANK-DAILY-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT CAST(PG.TxDate AS DATE) AS TxDate,
                SUM(ISNULL(PG.Debit, 0)) AS CashIn,
                SUM(ISNULL(PG.Credit, 0)) AS CashOut
            FROM PostGL PG
            {GlSqlHelper.BankJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {GlSqlHelper.BankFilter}
            GROUP BY CAST(PG.TxDate AS DATE)
            ORDER BY CAST(PG.TxDate AS DATE);
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd => InvNumSqlHelper.AddDateParameters(cmd, from, to));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            days = rows.Select(r => new
            {
                date = r["TxDate"] is DateTime d ? d.ToString("yyyy-MM-dd") : r["TxDate"]?.ToString(),
                cashIn = GlSqlHelper.ToDecimal(r, "CashIn"),
                cashOut = GlSqlHelper.ToDecimal(r, "CashOut"),
                net = GlSqlHelper.ToDecimal(r, "CashIn") - GlSqlHelper.ToDecimal(r, "CashOut")
            }),
            note = "Daily bank inflow vs outflow from PostGL.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
