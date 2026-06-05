using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

/// <summary>Product monthly order/sales analysis routing (SAGE-PATCH-009).</summary>
internal static class ProductOrderAnalysisChatMatcher
{
    public const string Operation = "product.monthly.orders.analysis";

    public static bool TryRoute(
        string message,
        string m,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;
        if (!IsProductMonthlyOrderQuery(m))
            return false;

        parameters["message"] = message;
        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();

        var year = ChatIntentMatcher.ExtractYearFromMessage(message);
        if (year.HasValue)
            parameters["year"] = year.Value.ToString();

        if (m.Contains("by value") || m.Contains("sales value") || (m.Contains("value") && m.Contains("most") && !m.Contains("quantity")))
            parameters["rankBy"] = "value";

        operation = Operation;
        tools.Add(operation);
        return true;
    }

    public static bool IsProductMonthlyOrderQuery(string m)
    {
        if (string.IsNullOrWhiteSpace(m))
            return false;

        if (m.Contains("customer") && !m.Contains("product") && !m.Contains("item") && !m.Contains("stock"))
            return false;

        if (m.Contains("supplier") || m.Contains("vat") || m.Contains("outstanding") || m.Contains("unpaid"))
            return false;

        var productContext = m.Contains("product") || m.Contains("item") || m.Contains("stock item") ||
                             (m.Contains("stock") && !m.Contains("inventory valuation"));

        var orderSales = m.Contains("ordered") || m.Contains("order") || m.Contains("sell") ||
                         m.Contains("sales") || m.Contains("sold") || m.Contains("bought") ||
                         m.Contains("buy") || m.Contains("frequently");

        var monthly = m.Contains("month") || m.Contains("per month") || m.Contains("monthly");

        var measure = m.Contains("quantity") || m.Contains("qty") || m.Contains("value");

        if (productContext && orderSales && (monthly || measure))
            return true;

        if (productContext && monthly && measure)
            return true;

        if (productContext && (m.Contains("ordered most") || m.Contains("sells most") || m.Contains("sell most")))
            return true;

        if ((m.Contains("which product") || m.Contains("which item")) &&
            (m.Contains("most") || m.Contains("top")))
            return true;

        return productContext && monthly && (m.Contains("analysis") || m.Contains("breakdown"));
    }
}
