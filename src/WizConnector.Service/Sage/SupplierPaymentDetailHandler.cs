using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-PAYMENT-BEHAVIOR-001 — AP payment discipline detail for a specific supplier.</summary>
internal static class SupplierPaymentDetailHandler
{
    public const string QuerySerial = SupplierPaymentBehaviorEngine.QuerySerial;

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var code = parameters.GetValueOrDefault("supplierCode") ?? "";
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Supplier code is required for payment behaviour detail.");

        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var (ok, error, metrics) = SupplierPaymentBehaviorEngine.LoadMetrics(connectionString, parameters, code);

        if (!ok)
        {
            return JsonSerializer.Serialize(new
            {
                querySerial = QuerySerial,
                supplierCode = code,
                lowConfidence = true,
                finding = error,
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
        }

        var m = metrics.FirstOrDefault();
        if (m is null)
        {
            return JsonSerializer.Serialize(new
            {
                querySerial = QuerySerial,
                supplierCode = code,
                finding = $"No purchase invoice history found for supplier {code} in the selected period.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = "supplier.payment.detail",
            supplier = SupplierPaymentPromptTopHandler.ToRow(1, m),
            finding = m.PaymentDisciplineScore >= 75
                ? $"{code} invoices are well managed ({m.PaymentDisciplineScore}/100 payment discipline)."
                : $"{code} has payment discipline concerns ({m.PaymentDisciplineScore}/100) — {m.OverdueInvoices} overdue invoice(s).",
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
