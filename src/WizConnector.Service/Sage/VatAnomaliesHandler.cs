using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class VatAnomaliesHandler
{
    public const string QuerySerial = "SAGE-VAT-ANOMALY-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 20);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var sql = $"""
            SELECT TOP (@pTop)
                H.InvNumber AS InvoiceNumber,
                H.AccountID AS AccountCode,
                CAST(H.InvDate AS DATE) AS InvoiceDate,
                ISNULL(H.InvTotIncl, 0) AS InvoiceTotal,
                ISNULL(H.InvTotTax, 0) AS VatAmount,
                CASE WHEN ISNULL(H.InvTotIncl, 0) <> 0
                    THEN ABS(ISNULL(H.InvTotTax, 0) / NULLIF(H.InvTotIncl, 0)) ELSE 0 END AS VatRatio
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND ({InvNumSqlHelper.SalesDocTypeFilter} OR {InvNumSqlHelper.PurchaseDocTypeFilter})
              AND ISNULL(H.InvTotIncl, 0) > 0
              AND (
                    ABS(ISNULL(H.InvTotTax, 0) / NULLIF(H.InvTotIncl, 0)) > 0.20
                    OR (ISNULL(H.InvTotTax, 0) = 0 AND ISNULL(H.InvTotIncl, 0) > 1000)
                  )
            ORDER BY ABS(ISNULL(H.InvTotTax, 0) / NULLIF(H.InvTotIncl, 0)) DESC;
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
            invoices = rows.Select((r, i) => new { rank = i + 1, invoiceNumber = r["InvoiceNumber"]?.ToString(), vatAmount = GlSqlHelper.ToDecimal(r, "VatAmount") }),
            note = "Invoices with unusual VAT ratio or zero VAT on large values.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
