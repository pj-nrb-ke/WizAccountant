using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class VatZeroRatedHandler
{
    public const string QuerySerial = "SAGE-VAT-ZERO-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 25);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT TOP (@pTop)
                H.InvNumber AS InvoiceNumber,
                H.AccountID AS AccountCode,
                CAST(H.InvDate AS DATE) AS InvoiceDate,
                ISNULL(H.InvTotIncl, 0) AS InvoiceTotal
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.SalesDocTypeFilter}
              AND ISNULL(H.InvTotTax, 0) = 0
              AND ISNULL(H.InvTotIncl, 0) > 0
            ORDER BY ISNULL(H.InvTotIncl, 0) DESC;
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
            invoices = rows.Select((r, i) => new { rank = i + 1, invoiceNumber = r["InvoiceNumber"]?.ToString(), invoiceTotal = GlSqlHelper.ToDecimal(r, "InvoiceTotal") }),
            note = "Zero-rated/exempt sales invoices (zero InvTotTax on sales DocType).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
