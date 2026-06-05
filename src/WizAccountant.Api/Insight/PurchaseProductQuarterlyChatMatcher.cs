using System.Text.RegularExpressions;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

/// <summary>Purchase quantity/value by calendar quarter for specific stock item(s) — delegates to SAGE-QUERY-001 builder.</summary>
internal static class PurchaseProductQuarterlyChatMatcher
{
    public const string Operation = DynamicAnalyticalQueryBuilder.PurchaseItemPeriodSummaryOperation;

    public static bool TryRoute(
        string message,
        string m,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;
        var contract = QueryIntentContract.Parse(message, SageIntentEngine.Classify(message));
        return DynamicAnalyticalQueryBuilder.TryPlan(message, contract, parameters, tools, out operation);
    }

    public static bool IsPurchaseProductQuarterlyQuery(string m) =>
        IsPurchaseProductQuarterlyQuery(m, m);

    public static bool IsPurchaseProductQuarterlyQuery(string message, string m)
    {
        var contract = QueryIntentContract.Parse(message, SageIntentEngine.Classify(message));
        return DynamicAnalyticalQueryBuilder.IsItemPurchaseByPeriod(message, m, contract);
    }
}
