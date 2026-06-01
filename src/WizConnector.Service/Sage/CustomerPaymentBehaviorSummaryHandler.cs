using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class CustomerPaymentBehaviorSummaryHandler
{
    public const string QuerySerial = CustomerPaymentBehaviorEngine.QuerySerial;

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var (ok, error, metrics) = CustomerPaymentBehaviorEngine.LoadMetrics(connectionString, parameters);

        if (!ok)
        {
            return JsonSerializer.Serialize(new
            {
                querySerial = QuerySerial,
                lowConfidence = true,
                finding = error,
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
        }

        var prompt = metrics.OrderByDescending(m => m.PaymentDisciplineScore).Take(5)
            .Select((m, i) => CustomerPaymentPromptTopHandler.ToRow(i + 1, m)).ToList();
        var slow = metrics.OrderBy(m => m.PaymentDisciplineScore).Take(5)
            .Select((m, i) => CustomerPaymentPromptTopHandler.ToRow(i + 1, m)).ToList();

        var avgCollection = metrics.Where(m => m.PaidInvoices > 0).Select(m => m.AvgPaymentDays).DefaultIfEmpty(0).Average();
        var totalOverdue = metrics.Sum(m => m.CurrentOverdue);

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = "customer.payment.behavior.summary",
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            promptPayers = prompt,
            slowPayers = slow,
            averageCollectionDays = Math.Round((decimal)avgCollection, 1),
            totalCurrentOverdueExposure = totalOverdue,
            customersAnalyzed = metrics.Count,
            finding = $"Payment behaviour summary for {metrics.Count} customers with invoice history in period.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
