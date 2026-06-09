using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-PAYMENT-BEHAVIOR-001 — suppliers we pay LEAST promptly (low paid%, high overdue).</summary>
internal static class SupplierPaymentLateTopHandler
{
    public const string QuerySerial = SupplierPaymentBehaviorEngine.QuerySerial;

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var (ok, error, metrics) = SupplierPaymentBehaviorEngine.LoadMetrics(connectionString, parameters);

        if (!ok)
            return SupplierPaymentPromptTopHandler.Execute(connectionString, parameters);

        var ranked = metrics
            .Where(m => m.InvoicesAnalyzed >= 1)
            .OrderBy(m => m.PaymentDisciplineScore)
            .ThenBy(m => m.PaidRatio)
            .ThenByDescending(m => m.AvgDaysOverdue)
            .Take(top)
            .Select((m, i) => SupplierPaymentPromptTopHandler.ToRow(i + 1, m))
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = "supplier.payment.late.top",
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            requestedTop = top,
            suppliers = ranked,
            finding = $"Suppliers we pay LEAST promptly — {ranked.Count} shown by payment discipline score (lowest first).",
            note = "These suppliers have the highest overdue exposure or lowest paid ratio in the period.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
