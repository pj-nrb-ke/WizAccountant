using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class VatByAccountTopHandler
{
    public const string QuerySerial = "SAGE-VAT-ACCT-TOP-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 10);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT TOP (@pTop)
                H.AccountID AS AccountCode,
                SUM(ISNULL(H.InvTotTax, 0)) AS VatTotal
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.SalesDocTypeFilter}
              AND ISNULL(H.InvTotTax, 0) <> 0
            GROUP BY H.AccountID
            ORDER BY SUM(ISNULL(H.InvTotTax, 0)) DESC;
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
            accounts = GlExpenseTopHandler.MapRanked(rows, "VatTotal"),
            note = "Top customers by output VAT on sales invoices.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
