using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class VatOutputHandler
{
    public const string QuerySerial = "SAGE-VAT-OUTPUT-001";

    private const string Sql = $"""
        SELECT ISNULL(SUM(ISNULL(H.InvTotTax, 0) + ISNULL(H.Tax, 0)), 0)
        FROM InvNum H
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND {InvNumSqlHelper.SalesDocTypeFilter};
        """;

    public static decimal RunOutputTotal(string connectionString, DateTime from, DateTime to) =>
        VatSqlHelper.RunScalar(connectionString, Sql, from, to);

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = VatSqlHelper.ParsePeriod(parameters);
        var total = RunOutputTotal(connectionString, from, to);
        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            outputVat = total,
            countOnly = true,
            aggregationMode = true,
            finding = $"Output VAT collected: {total:N2}.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
