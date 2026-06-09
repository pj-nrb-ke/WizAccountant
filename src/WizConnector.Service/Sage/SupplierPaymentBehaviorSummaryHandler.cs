using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-PAYMENT-BEHAVIOR-001 — overall AP payment discipline summary.</summary>
internal static class SupplierPaymentBehaviorSummaryHandler
{
    public const string QuerySerial = SupplierPaymentBehaviorEngine.QuerySerial;

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var (ok, error, metrics) = SupplierPaymentBehaviorEngine.LoadMetrics(connectionString, parameters);

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

        var prompt = metrics
            .OrderByDescending(m => m.PaymentDisciplineScore)
            .Take(5)
            .Select((m, i) => SupplierPaymentPromptTopHandler.ToRow(i + 1, m))
            .ToList();

        var slow = metrics
            .OrderBy(m => m.PaymentDisciplineScore)
            .Take(5)
            .Select((m, i) => SupplierPaymentPromptTopHandler.ToRow(i + 1, m))
            .ToList();

        var avgDaysOverdue = metrics
            .Where(m => m.OverdueInvoices > 0)
            .Select(m => (double)m.AvgDaysOverdue)
            .DefaultIfEmpty(0)
            .Average();

        var totalOverdueExposure  = metrics.Sum(m => m.CurrentOverdue);
        var totalPaid             = metrics.Sum(m => m.PaidInvoices);
        var totalAnalyzed         = metrics.Sum(m => m.InvoicesAnalyzed);
        var overallPaidPct        = totalAnalyzed > 0 ? Math.Round((double)totalPaid / totalAnalyzed * 100, 1) : 0.0;

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = "supplier.payment.behavior.summary",
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            promptPayers = prompt,
            slowPayers = slow,
            averageDaysOverdue = Math.Round((decimal)avgDaysOverdue, 1),
            overallPaidPercent = overallPaidPct,
            totalCurrentOverdueExposure = totalOverdueExposure,
            suppliersAnalyzed = metrics.Count,
            finding = $"AP payment discipline summary: {overallPaidPct}% of invoices cleared for {metrics.Count} suppliers in period.",
            note = "Paid = Outstanding ≤ 0.01. Overdue = Outstanding > 0.01 and past DueDate.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
