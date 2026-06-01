using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class CustomerPaymentLateTopHandler
{
    public const string QuerySerial = CustomerPaymentBehaviorEngine.QuerySerial;

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var (ok, error, metrics) = CustomerPaymentBehaviorEngine.LoadMetrics(connectionString, parameters);

        if (!ok)
            return CustomerPaymentPromptTopHandler.Execute(connectionString, parameters);

        var ranked = metrics
            .Where(m => m.PaidInvoices >= 1)
            .OrderBy(m => m.PaymentDisciplineScore)
            .ThenBy(m => m.PromptPaymentRatio)
            .ThenByDescending(m => m.AvgDaysLate)
            .Take(top)
            .Select((m, i) => CustomerPaymentPromptTopHandler.ToRow(i + 1, m))
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = "customer.payment.late.top",
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            requestedTop = top,
            customers = ranked,
            finding = $"Slowest / latest-paying customers by payment discipline score ({ranked.Count} shown).",
            note = "Ranked by worst payment discipline — not highest current outstanding.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
