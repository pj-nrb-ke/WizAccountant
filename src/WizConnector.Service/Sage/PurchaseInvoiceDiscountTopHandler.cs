using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-PUR-DISC-TOP-001 — top purchase invoices by discount value.</summary>
internal static class PurchaseInvoiceDiscountTopHandler
{
    public const string QuerySerial = "SAGE-PUR-DISC-TOP-001";

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
                H.AccountID AS SupplierCode,
                {discExpr} AS DiscountValue
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.PurchaseDocTypeFilter}
              AND {InvNumSqlHelper.DiscountPredicate}
            ORDER BY {discExpr} DESC;
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
                discountValue = Convert.ToDecimal(reader["DiscountValue"])
            });
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            topInvoices = rows,
            note = "Top purchase invoices by discount on InvNum — not salesinvoice.discount.top.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
