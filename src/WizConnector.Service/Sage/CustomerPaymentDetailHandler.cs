using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class CustomerPaymentDetailHandler
{
    public const string QuerySerial = CustomerPaymentBehaviorEngine.QuerySerial;

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var code = parameters.GetValueOrDefault("customerCode") ?? "";
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Customer code is required for payment behaviour detail.");

        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var (ok, error, metrics) = CustomerPaymentBehaviorEngine.LoadMetrics(connectionString, parameters, code);

        if (!ok)
        {
            return JsonSerializer.Serialize(new
            {
                querySerial = QuerySerial,
                customerCode = code,
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
                customerCode = code,
                finding = $"No paid invoice history found for customer {code} in the selected period.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = "customer.payment.detail",
            customer = CustomerPaymentPromptTopHandler.ToRow(1, m),
            finding = m.PaymentDisciplineScore >= 75
                ? $"{code} shows good payment discipline ({m.PaymentDisciplineScore}/100)."
                : $"{code} shows weak payment discipline ({m.PaymentDisciplineScore}/100).",
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
