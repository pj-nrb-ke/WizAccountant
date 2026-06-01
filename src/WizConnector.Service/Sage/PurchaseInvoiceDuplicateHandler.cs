using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-DUPLICATE-INV-001 — duplicate supplier invoices (SQL on InvNum).</summary>
internal static class PurchaseInvoiceDuplicateHandler
{
    public const string QuerySerial = "SAGE-AP-DUPLICATE-INV-001";

    private static string BuildSql() => $"""
        SELECT TOP (@pTop)
            H.AccountID AS SupplierCode,
            H.InvNumber AS InvoiceNumber,
            CAST(H.InvDate AS DATE) AS InvoiceDate,
            H.InvTotIncl AS InvoiceTotal,
            COUNT(*) AS DuplicateCount
        FROM InvNum H
        WHERE {InvNumSqlHelper.PurchaseDocTypeFilter}
        GROUP BY H.AccountID, H.InvNumber, CAST(H.InvDate AS DATE), H.InvTotIncl
        HAVING COUNT(*) > 1
        ORDER BY COUNT(*) DESC, H.InvTotIncl DESC;
        """;

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var rows = new List<object>();
        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(BuildSql(), conn) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@pTop", top);
        using var reader = cmd.ExecuteReader();
        var rank = 0;
        while (reader.Read())
        {
            rank++;
            rows.Add(new
            {
                rank,
                supplierCode = reader["SupplierCode"]?.ToString(),
                invoiceNumber = reader["InvoiceNumber"]?.ToString(),
                invoiceDate = reader["InvoiceDate"] is DateTime dt ? dt.ToString("yyyy-MM-dd") : reader["InvoiceDate"]?.ToString(),
                invoiceTotal = reader["InvoiceTotal"] is DBNull ? 0m : Convert.ToDecimal(reader["InvoiceTotal"]),
                duplicateCount = Convert.ToInt32(reader["DuplicateCount"])
            });
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            duplicates = rows,
            countOnly = false,
            note = "Same supplier + invoice number + date + total appearing more than once on InvNum (purchase).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
