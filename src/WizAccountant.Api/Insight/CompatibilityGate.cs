namespace WizAccountant.Api.Insight;

/// <summary>Validates route/handler vs parsed intent contract before Sage execution (Layer 4).</summary>
internal static class CompatibilityGate
{
    public static bool IsCompatible(QueryIntentContract contract, string operation, out string? reason)
    {
        reason = null;
        var cap = HandlerCapabilityRegistry.Get(operation);

        if (contract.Groupings.Contains("product") && contract.Groupings.Contains("month"))
        {
            if (operation is "customer.unpaid.summary" or "customer.openitems" or "customer.list" or "customer.sales.top")
            {
                reason = "customer-listing cannot satisfy product-by-month analysis";
                return false;
            }

            if (contract.Metrics.Contains("quantity") && contract.Metrics.Contains("value"))
            {
                if (cap is not null && !cap.SupportsMonthlyBreakdown)
                {
                    reason = $"handler {operation} does not support monthly product breakdown";
                    return false;
                }

                if (string.Equals(operation, "inventory.movement.top", StringComparison.OrdinalIgnoreCase))
                {
                    reason = "inventory.movement.top lacks monthly quantity+value breakdown";
                    return false;
                }
            }
        }

        if (contract.OutputShape.Contains("explainability") && cap is not null && !cap.SupportsExplainability)
        {
            if (operation is "customer.list" or "supplier.list" or "inventoryitem.list")
            {
                reason = "listing handler cannot satisfy explainability request";
                return false;
            }
        }

        if (contract.Metrics.Contains("vat") && operation is "customer.sales.top" or "salesinvoice.discount.top")
        {
            reason = "sales ranking is not VAT analysis";
            return false;
        }

        return true;
    }

    public static string? SuggestCanonicalOperation(QueryIntentContract contract)
    {
        if (contract.Groupings.Contains("product") &&
            (contract.Groupings.Contains("month") || contract.Metrics.Count >= 2))
            return ProductOrderAnalysisChatMatcher.Operation;

        if (CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(contract.RawQuery.ToLowerInvariant()))
            return "customer.payment.prompt.top";

        return null;
    }
}
