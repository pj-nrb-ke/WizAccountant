using System.Text.RegularExpressions;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

/// <summary>
/// SAGE-QUERY-001 — controlled analytical templates when no dedicated static handler matched.
/// Not free-form SQL; maps QueryIntentContract to allowlisted connector operations.
/// </summary>
internal static class DynamicAnalyticalQueryBuilder
{
    public const string PurchaseItemPeriodSummaryOperation = "purchase.item.period.summary";

    /// <summary>Legacy alias — same template.</summary>
    public const string PurchaseProductQuarterlyOperation = "purchase.product.quarterly";

    public static bool TryPlan(
        string message,
        QueryIntentContract contract,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;
        var m = message.ToLowerInvariant();

        if (TryItemPurchaseByPeriod(message, m, contract, parameters, out operation))
        {
            tools.Add($"dynamic:{operation}");
            tools.Add(operation!);
            return true;
        }

        if (TryItemSalesByPeriod(message, m, contract, parameters, out operation))
        {
            tools.Add($"dynamic:{operation}");
            tools.Add(operation!);
            return true;
        }

        return false;
    }

    public static bool CanAnswer(string message, QueryIntentContract contract)
    {
        var m = message.ToLowerInvariant();
        return IsItemPurchaseByPeriod(message, m, contract) || IsItemSalesByPeriod(message, m, contract);
    }

    private static bool TryItemPurchaseByPeriod(
        string message,
        string m,
        QueryIntentContract contract,
        Dictionary<string, string> parameters,
        out string? operation)
    {
        operation = null;
        if (!IsItemPurchaseByPeriod(message, m, contract))
            return false;

        operation = PurchaseItemPeriodSummaryOperation;
        parameters["message"] = message;
        ApplyItemFilters(message, m, parameters);
        ApplyPeriodGroupBy(m, parameters);
        ApplyPeriod(message, parameters, contract);
        return true;
    }

    private static bool TryItemSalesByPeriod(
        string message,
        string m,
        QueryIntentContract contract,
        Dictionary<string, string> parameters,
        out string? operation)
    {
        operation = null;
        if (!IsItemSalesByPeriod(message, m, contract))
            return false;

        if (ProductOrderAnalysisChatMatcher.TryRoute(message, m, parameters, new List<string>(), out operation))
            return true;

        operation = ProductOrderAnalysisChatMatcher.Operation;
        parameters["message"] = message;
        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();
        var year = ChatIntentMatcher.ExtractYearFromMessage(message);
        if (year.HasValue)
            parameters["year"] = year.Value.ToString();
        if (m.Contains("by value") || (m.Contains("value") && !m.Contains("quantity")))
            parameters["rankBy"] = "value";
        return true;
    }

    internal static bool IsItemPurchaseByPeriod(string message, string m, QueryIntentContract contract)
    {
        if (m.Contains("supplier credit") || m.Contains("credit note"))
            return false;
        if (m.Contains("sold") || m.Contains("customer sales"))
            return false;

        var purchase = m.Contains("bought") || m.Contains("buy") || m.Contains("purchase") ||
                       m.Contains("purchased") || m.Contains("procured");
        var period = HasPeriodGrouping(m, contract);
        var item = HasItemSignal(m, message);

        return purchase && period && item;
    }

    internal static bool IsItemSalesByPeriod(string message, string m, QueryIntentContract contract)
    {
        if (m.Contains("bought") || m.Contains("purchase"))
            return false;

        var sales = m.Contains("sold") || m.Contains("sales") || m.Contains("sell") ||
                    m.Contains("ordered") || m.Contains("revenue");
        var period = HasPeriodGrouping(m, contract) ||
                     m.Contains("month") || m.Contains("monthly") || m.Contains("quarter");
        var item = HasItemSignal(m, message) || m.Contains("product") || m.Contains("item");

        return sales && period && item;
    }

    private static bool HasPeriodGrouping(string m, QueryIntentContract contract) =>
        m.Contains("quarter") || m.Contains("per quarter") || m.Contains("by quarter") ||
        m.Contains("per month") || m.Contains("by month") || m.Contains("monthly") ||
        Regex.IsMatch(m, @"\bq[1-4]\b") ||
        contract.Period?.Segments.Count > 0;

    private static bool HasItemSignal(string m, string message) =>
        m.Contains("cpo") || m.Contains("crude palm oil") || m.Contains("palm oil") ||
        m.Contains("item") || m.Contains("stock") ||
        Regex.IsMatch(message, @"\b[A-Z]{2,}[A-Z0-9]*\d+\b");

    private static void ApplyItemFilters(string message, string m, Dictionary<string, string> parameters)
    {
        var codes = Regex.Matches(message, @"\b[A-Z]{2,}[A-Z0-9]*\d+\b")
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codes.Count > 0)
        {
            parameters["productCodes"] = string.Join(",", codes);
            return;
        }

        if (m.Contains("cpo") || m.Contains("crude palm oil") || m.Contains("palm oil"))
            parameters["productSearch"] = "Palm Oil";
    }

    private static void ApplyPeriodGroupBy(string m, Dictionary<string, string> parameters)
    {
        parameters["groupBy"] = m.Contains("by month") || m.Contains("per month") || m.Contains("monthly")
            ? "month"
            : "quarter";
    }

    private static void ApplyPeriod(string message, Dictionary<string, string> parameters, QueryIntentContract contract)
    {
        var year = ChatIntentMatcher.ExtractYearFromMessage(message) ?? contract.YearHint;
        if (year.HasValue)
            parameters["year"] = year.Value.ToString();

        InsightChatPeriodHelper.TryApplyForOperation(
            PurchaseItemPeriodSummaryOperation,
            message,
            parameters,
            contract,
            out _);
    }
}
