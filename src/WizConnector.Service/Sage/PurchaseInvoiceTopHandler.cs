using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-PUR-INV-TOP-001 — top N purchase invoices by value.</summary>
internal static class PurchaseInvoiceTopHandler
{
    public const string QuerySerial = "SAGE-PUR-INV-TOP-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var sql = $"""
            SELECT TOP (@pTop)
                H.InvNumber AS InvoiceNumber,
                H.AccountID AS SupplierCode,
                CAST(H.InvDate AS DATE) AS InvoiceDate,
                ISNULL(H.InvTotIncl, 0) AS InvoiceTotal
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.PurchaseDocTypeFilter}
            ORDER BY ISNULL(H.InvTotIncl, 0) DESC;
            """;

        var rows = new List<object>();
        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@pTop", top);
        InvNumSqlHelper.AddDateParameters(cmd, from, to);
        using var reader = cmd.ExecuteReader();
        var rank = 0;
        while (reader.Read())
        {
            rank++;
            rows.Add(new
            {
                rank,
                invoiceNumber = reader["InvoiceNumber"]?.ToString(),
                supplierCode = reader["SupplierCode"]?.ToString(),
                invoiceDate = reader["InvoiceDate"] is DateTime d ? d.ToString("yyyy-MM-dd") : "",
                invoiceTotal = Convert.ToDecimal(reader["InvoiceTotal"])
            });
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            topInvoices = rows,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            note = "Top purchase invoices by InvTotIncl in period.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
