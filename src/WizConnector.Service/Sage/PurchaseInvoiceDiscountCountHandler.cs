using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-PUR-DISC-COUNT-001 — count purchase invoices with discounts (InvNum).</summary>
internal static class PurchaseInvoiceDiscountCountHandler
{
    public const string QuerySerial = "SAGE-PUR-DISC-COUNT-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var sql = $"""
            SELECT COUNT(DISTINCT H.AutoIndex) AS InvoiceCount
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.PurchaseDocTypeFilter}
              AND {InvNumSqlHelper.DiscountPredicate};
            """;

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
        InvNumSqlHelper.AddDateParameters(cmd, from, to);
        var count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            invoiceCount = count,
            countOnly = true,
            aggregationMode = true,
            finding = $"Purchase invoices with discounts in period: {count:N0}.",
            note = "COUNT on InvNum purchase invoices with discount fields — not sales discount handler.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
