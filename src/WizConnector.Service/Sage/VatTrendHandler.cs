using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class VatTrendHandler
{
    public const string QuerySerial = "SAGE-VAT-TREND-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT YEAR(H.InvDate) AS Yr, MONTH(H.InvDate) AS Mo,
                SUM(CASE WHEN {InvNumSqlHelper.SalesDocTypeFilter} THEN ISNULL(H.InvTotTax, 0) ELSE 0 END) AS OutputVat,
                SUM(CASE WHEN {InvNumSqlHelper.PurchaseDocTypeFilter} THEN ISNULL(H.InvTotTax, 0) ELSE 0 END) AS InputVat
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
            GROUP BY YEAR(H.InvDate), MONTH(H.InvDate)
            ORDER BY YEAR(H.InvDate), MONTH(H.InvDate);
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd => InvNumSqlHelper.AddDateParameters(cmd, from, to));
        var months = rows.Select(r => new
        {
            period = $"{r["Yr"]}-{Convert.ToInt32(r["Mo"]):D2}",
            outputVat = GlSqlHelper.ToDecimal(r, "OutputVat"),
            inputVat = GlSqlHelper.ToDecimal(r, "InputVat"),
            payable = GlSqlHelper.ToDecimal(r, "OutputVat") - GlSqlHelper.ToDecimal(r, "InputVat")
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            months,
            note = "Monthly VAT trend from InvNum tax fields.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
