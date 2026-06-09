using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-PAYMENT-BEHAVIOR-001 — suppliers we pay most promptly (high paid%, low overdue).</summary>
internal static class SupplierPaymentPromptTopHandler
{
    public const string QuerySerial = SupplierPaymentBehaviorEngine.QuerySerial;

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var (ok, error, metrics) = SupplierPaymentBehaviorEngine.LoadMetrics(connectionString, parameters);

        if (!ok)
            return BuildFallback(error ?? "Supplier payment discipline SQL unavailable.", from, to);

        var ranked = metrics
            .Where(m => m.PaidInvoices >= 1)
            .OrderByDescending(m => m.PaymentDisciplineScore)
            .ThenByDescending(m => m.PaidRatio)
            .ThenBy(m => m.AvgDaysOverdue)
            .Take(top)
            .Select((m, i) => ToRow(i + 1, m))
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = "supplier.payment.prompt.top",
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            requestedTop = top,
            suppliers = ranked,
            finding = ranked.Count > 0
                ? $"Top {ranked.Count} suppliers we pay most promptly by payment discipline score."
                : "No suppliers with enough invoice history in the selected period.",
            note = "Ranked by payment discipline (paid% and overdue exposure). Higher score = we pay this supplier on time.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    internal static object ToRow(int rank, SupplierPaymentBehaviorEngine.SupplierPaymentMetrics m) => new
    {
        rank,
        code = m.Code,
        name = m.Name,
        paymentDisciplineScore = m.PaymentDisciplineScore,
        paidPercent = Math.Round(m.PaidRatio * 100, 1),
        overdueInvoices = m.OverdueInvoices,
        averageDaysOverdue = Math.Round(m.AvgDaysOverdue, 1),
        currentOverdueBalance = m.CurrentOverdue,
        invoicesAnalyzed = m.InvoicesAnalyzed,
        paidInvoices = m.PaidInvoices
    };

    private static string BuildFallback(string error, DateTime from, DateTime to) =>
        JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            lowConfidence = true,
            finding = "Supplier payment discipline analysis requires InvNum purchase invoices (DocType 5) and Vendor table.",
            status = error,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
}
