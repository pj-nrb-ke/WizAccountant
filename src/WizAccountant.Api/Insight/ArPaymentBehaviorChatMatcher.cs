namespace WizAccountant.Api.Insight;

/// <summary>Routes customer payment discipline analytics (SAGE-TRAIN-008).</summary>
internal static class ArPaymentBehaviorChatMatcher
{
    public static bool TryRoute(
        string message,
        string m,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;
        if (!CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(m))
            return false;

        parameters["message"] = message;
        var top = ChatIntentMatcher.ResolveTopCount(m, 10);
        parameters["top"] = top.ToString();
        parameters["minInvoices"] = "3";

        var year = ChatIntentMatcher.ExtractYearFromMessage(message);
        if (year.HasValue)
            parameters["year"] = year.Value.ToString();

        ExtractCustomerCode(message, parameters);

        if (CustomerPaymentBehaviorHelper.IsPaymentSummaryQuery(m))
        {
            operation = "customer.payment.behavior.summary";
            tools.Add(operation);
            return true;
        }

        if (CustomerPaymentBehaviorHelper.IsLatePayerQuery(m))
        {
            operation = "customer.payment.late.top";
            tools.Add(operation);
            return true;
        }

        if (CustomerPaymentBehaviorHelper.IsPaymentDetailQuery(m) && parameters.ContainsKey("customerCode"))
        {
            operation = "customer.payment.detail";
            tools.Add(operation);
            return true;
        }

        operation = "customer.payment.prompt.top";
        tools.Add(operation);
        return true;
    }

    private static void ExtractCustomerCode(string message, Dictionary<string, string> parameters)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            message,
            @"(?:customer|for)\s+([A-Z0-9][A-Z0-9\-]{1,15})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
            parameters["customerCode"] = match.Groups[1].Value.ToUpperInvariant();
    }
}
