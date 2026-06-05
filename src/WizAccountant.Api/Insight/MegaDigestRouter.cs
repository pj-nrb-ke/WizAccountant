namespace WizAccountant.Api.Insight;

/// <summary>Routes user messages via 500-query mega digest semantic match.</summary>
internal static class MegaDigestRouter
{
    public static bool TryPlan(
        string message,
        string messageLower,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;

        // Let dedicated matchers win when they already apply
        if (ChatIntentMatcher.IsSalesInvoiceDiscountCountQuery(messageLower) ||
            ChatIntentMatcher.IsCustomerAgedTopQuery(messageLower) ||
            ChatIntentMatcher.IsCustomerUnpaidSummaryQuery(messageLower) ||
            ChatIntentMatcher.IsSupplierUnpaidQuery(messageLower) ||
            ChatIntentMatcher.IsUnpaidSalesInvoiceQuery(messageLower) ||
            ChatIntentMatcher.IsInventoryBsNegativeLedgersQuery(messageLower) ||
            SageChatDomain.IsInventoryGlReconciliationQuestion(messageLower) ||
            InventoryFixWorkflow.IsFixWorkflowRequest(messageLower))
            return false;

        var entry = MegaDigestCatalog.Instance.FindBestMatch(message);
        if (entry is null)
            return false;

        var resolved = MegaDigestOperationResolver.Resolve(entry, messageLower);
        if (string.IsNullOrEmpty(resolved.Operation) || !resolved.Implemented)
            return false;

        operation = resolved.Operation;
        parameters["digestId"] = entry.Id.ToString();
        parameters["digestTitle"] = entry.Title;
        if (!string.IsNullOrEmpty(resolved.DigestNote))
            parameters["digestNote"] = resolved.DigestNote;

        ApplyParametersFromMessage(messageLower, entry, parameters);

        tools.Add(operation);
        return true;
    }

    public static string? BuildUnmatchedHint(string message)
    {
        var entry = MegaDigestCatalog.Instance.FindBestMatch(message, minScore: 2);
        if (entry is null)
            return null;

        var resolved = MegaDigestOperationResolver.Resolve(entry, message.ToLowerInvariant());
        if (resolved.Implemented && !string.IsNullOrEmpty(resolved.Operation))
        {
            return $"Your question is close to digest query #{entry.Id}: \"{entry.Title}\". " +
                   $"Try rephrasing or ask: \"{entry.Title}\"";
        }

        return $"Closest catalog query (#{entry.Id}): \"{entry.Title}\" — {resolved.DigestNote}";
    }

    private static void ApplyParametersFromMessage(string messageLower, MegaDigestEntry entry, Dictionary<string, string> parameters)
    {
        var top = ChatIntentMatcher.ResolveTopCount(messageLower, defaultTop: 0);
        if (top > 0)
            parameters["top"] = top.ToString();
        else if (entry.Title.Contains("top 10", StringComparison.OrdinalIgnoreCase))
            parameters["top"] = "10";
        else if (entry.Title.Contains("top 20", StringComparison.OrdinalIgnoreCase))
            parameters["top"] = "20";
        else if (entry.Title.Contains("top 5", StringComparison.OrdinalIgnoreCase))
            parameters["top"] = "5";

        if (messageLower.Contains("180") || entry.Title.Contains("180"))
            parameters["minDaysOutstanding"] = "180";
        else if (messageLower.Contains("90") || entry.Title.Contains("90 day"))
            parameters["minDaysOutstanding"] = "90";
    }
}
