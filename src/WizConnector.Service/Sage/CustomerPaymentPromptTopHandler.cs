using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class CustomerPaymentPromptTopHandler
{
    public const string QuerySerial = CustomerPaymentBehaviorEngine.QuerySerial;

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var (ok, error, metrics) = CustomerPaymentBehaviorEngine.LoadMetrics(connectionString, parameters);

        if (!ok)
            return BuildFallback(error ?? "Payment behavior SQL unavailable.", from, to);

        var ranked = metrics
            .Where(m => m.PaidInvoices >= 1)
            .OrderByDescending(m => m.PaymentDisciplineScore)
            .ThenByDescending(m => m.PromptPaymentRatio)
            .ThenBy(m => m.AvgDaysLate)
            .Take(top)
            .Select((m, i) => ToRow(i + 1, m))
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = "customer.payment.prompt.top",
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            requestedTop = top,
            customers = ranked,
            finding = ranked.Count > 0
                ? $"Top {ranked.Count} prompt-paying customers by payment discipline score (not current outstanding balance)."
                : "No customers with enough paid invoice history in the selected period.",
            note = "Based on InvNum invoice dates/due dates and PostAR payment (credit) dates. Not ranked by open AR balance.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    internal static object ToRow(int rank, CustomerPaymentBehaviorEngine.CustomerPaymentMetrics m) => new
    {
        rank,
        code = m.Code,
        name = m.Name,
        paymentDisciplineScore = m.PaymentDisciplineScore,
        paidWithinTermsPercent = Math.Round(m.PromptPaymentRatio * 100, 1),
        averagePaymentDays = Math.Round(m.AvgPaymentDays, 1),
        averageDaysLate = Math.Round(m.AvgDaysLate, 1),
        currentOverdueBalance = m.CurrentOverdue,
        invoicesAnalyzed = m.InvoicesAnalyzed,
        paidInvoices = m.PaidInvoices
    };

    private static string BuildFallback(string error, DateTime from, DateTime to) =>
        JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            lowConfidence = true,
            finding = "I understand this as customer payment behaviour analysis. " +
                      "This requires invoice due dates and payment allocation dates. " +
                      "Current outstanding balance alone cannot identify prompt payers.",
            status = error,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
}
