using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-SALES-INV-DISC-TOP-001 — top N sales invoices by discount value (InvNum).</summary>
internal static class SalesInvoiceDiscountTopHandler
{
    public const string QuerySerial = "SAGE-SALES-INV-DISC-TOP-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var top = InvNumSqlHelper.ParseTop(parameters, 5);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var discExpr = InvNumSqlHelper.TotalDiscountExpression;

        var sql = $"""
            SELECT TOP (@pTop)
                H.InvNumber AS InvoiceNumber,
                CAST(H.InvDate AS DATE) AS InvoiceDate,
                {discExpr} AS DiscountValue,
                ISNULL(H.InvTotIncl, 0) AS InvoiceTotal
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
              AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.SalesDocTypeFilter}
              AND {InvNumSqlHelper.DiscountPredicate}
            ORDER BY {discExpr} DESC, H.InvDate DESC;
            """;

        var rows = new List<object>();
        using (var conn = new SqlConnection(companyConnectionString))
        {
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
                    invoiceNumber = reader["InvoiceNumber"]?.ToString() ?? "",
                    invoiceDate = reader["InvoiceDate"] is DateTime d ? d.ToString("yyyy-MM-dd") : reader["InvoiceDate"]?.ToString(),
                    discountValue = Convert.ToDecimal(reader["DiscountValue"]),
                    invoiceTotal = Convert.ToDecimal(reader["InvoiceTotal"])
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            topInvoices = rows,
            countOnly = false,
            note = "Top invoices by header discount value on InvNum — not open AR listing.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
