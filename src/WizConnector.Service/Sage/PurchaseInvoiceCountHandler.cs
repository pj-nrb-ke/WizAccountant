using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-PUR-INV-COUNT-001 — count purchase invoices posted in period (InvNum SQL).</summary>
internal static class PurchaseInvoiceCountHandler
{
    public const string QuerySerial = "SAGE-PUR-INV-COUNT-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var sql = $"""
            SELECT COUNT(DISTINCT H.AutoIndex) AS InvoiceCount
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.PurchaseDocTypeFilter};
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
            finding = $"Total purchase invoices in period: {count:N0}.",
            note = "COUNT on InvNum purchase DocType — not supplier open-items listing.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
