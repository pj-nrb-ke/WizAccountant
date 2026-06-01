using System.Text.RegularExpressions;

namespace WizAccountant.Api.Insight;

/// <summary>Unified chat operation routing with business-process classification first (SAGE-CONSOLIDATION-001).</summary>
internal static class ChatRoutePlanner
{
    public static (string? operation, Dictionary<string, string> parameters, List<string> tools) Plan(
        string message,
        SageIntentEngine.Classification classification,
        InvestigationContext? investigation = null)
    {
        var m = message.ToLowerInvariant();
        var tools = new List<string> { $"intent:{classification.PrimaryIntent}" };
        var parameters = new Dictionary<string, string> { ["top"] = "500" };
        RankingQueryPolicy.ApplyRowLimits(message, classification, parameters);

        var bp = BusinessProcessClassifier.Classify(message);
        if (bp.Process != BusinessProcessType.Unknown)
            tools.Add($"businessProcess:{bp.Process}:{bp.Confidence:F2}");
        if (!string.IsNullOrEmpty(bp.BusinessMeaning))
            tools.Add($"businessMeaning:{bp.BusinessMeaning}");

        investigation?.ApplyFollowUp(message, parameters);

        if (TryCanonicalEarlyRoute(message, m, bp, parameters, tools, out var earlyOp))
        {
            TagInferredBusinessProcess(earlyOp, tools);
            return Finalize(message, earlyOp, parameters, tools);
        }

        var (operation, parameters2, tools2) = RouteMatchers(message, m, classification, parameters, tools);
        return Finalize(message, operation, parameters2, tools2);
    }

    private static bool TryCanonicalEarlyRoute(
        string message,
        string m,
        BusinessProcessClassifier.Classification bp,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;
        if (string.IsNullOrEmpty(bp.CanonicalOperation))
            return false;

        if (bp.Process == BusinessProcessType.PaymentBehavior &&
            ArPaymentBehaviorChatMatcher.TryRoute(message, m, parameters, tools, out operation))
            return true;

        if (bp.Process == BusinessProcessType.CashflowIntelligence &&
            GlFinanceChatMatcher.TryRoute(message, m, parameters, tools, out operation))
            return true;

        if (bp.Process == BusinessProcessType.InventoryLifecycle &&
            InvWarehouseChatMatcher.TryRoute(message, m, parameters, tools, out operation))
            return true;

        if (bp.Process is BusinessProcessType.Reconciliation or BusinessProcessType.BankReconciliation &&
            ReconciliationChatMatcher.TryRoute(message, m, parameters, tools, out operation))
            return true;

        if (bp.Process == BusinessProcessType.VatCompliance &&
            GlFinanceChatMatcher.TryRoute(message, m, parameters, tools, out operation))
            return true;

        if (InsightReadOnlyTools.IsAllowed(bp.CanonicalOperation))
        {
            operation = bp.CanonicalOperation;
            tools.Add($"canonical:{operation}");
            return true;
        }

        return false;
    }

    private static (string? operation, Dictionary<string, string> parameters, List<string> tools) RouteMatchers(
        string message,
        string m,
        SageIntentEngine.Classification classification,
        Dictionary<string, string> parameters,
        List<string> tools)
    {
        if ((m.Contains("dashboard") || m.Contains("kpi")) && !m.Contains("treasury") &&
            !m.Contains("vat") && !m.Contains("expense trend"))
        {
            tools.Add("dashboard.summary");
            return ("dashboard.summary", parameters, tools);
        }

        if (m.StartsWith("search ") || m.Contains("find "))
        {
            var query = Regex.Replace(message, "(?i)^(search|find)\\s+", "").Trim();
            parameters["query"] = query;
            tools.Add("search.global");
            return ("search.global", parameters, tools);
        }

        if (ChatIntentMatcher.TryInventoryBsNegativeLedgers(m, parameters, tools, out var negStockOp))
            return (negStockOp, parameters, tools);

        if (ReconciliationChatMatcher.TryRoute(message, m, parameters, tools, out var reconOp))
            return (reconOp, parameters, tools);

        if (ArPaymentBehaviorChatMatcher.TryRoute(message, m, parameters, tools, out var payBehOp))
            return (payBehOp, parameters, tools);

        if (ProductOrderAnalysisChatMatcher.TryRoute(message, m, parameters, tools, out var productMonthlyOp))
            return (productMonthlyOp, parameters, tools);

        if (ArSalesChatMatcher.TryRoute(message, m, parameters, tools, out var arSalesOp))
            return (arSalesOp, parameters, tools);

        if (ApPurchaseInvChatMatcher.TryRoute(message, m, parameters, tools, out var apOp))
            return (apOp, parameters, tools);

        if (InvWarehouseChatMatcher.TryRoute(message, m, parameters, tools, out var invOp))
            return (invOp, parameters, tools);

        if (GlFinanceChatMatcher.TryRoute(message, m, parameters, tools, out var finOp))
            return (finOp, parameters, tools);

        if (ChatIntentMatcher.TryCustomerAgedTop(m, parameters, tools, out var agedTopOp))
            return (agedTopOp, parameters, tools);

        if (ChatIntentMatcher.TryCustomerUnpaidSummary(m, parameters, tools, out var summaryOp))
            return (summaryOp, parameters, tools);

        if (SageChatDomain.TryGlTransactionList(m, parameters, tools, out var glOp) &&
            !m.Contains("expense") && !m.Contains("vat") && !m.Contains("treasury"))
            return (glOp, parameters, tools);

        if (ChatIntentMatcher.TryUnpaidSalesInvoices(m, parameters, tools, out var unpaidOp))
            return (unpaidOp, parameters, tools);

        if (MegaDigestRouter.TryPlan(message, m, parameters, tools, out var digestOp))
            return (digestOp, parameters, tools);

        if (m.Contains("open item") && m.Contains("supplier"))
        {
            ExtractAccount(m, parameters);
            tools.Add("supplier.openitems");
            return ("supplier.openitems", parameters, tools);
        }

        if ((m.Contains("open item") || m.Contains("outstanding")) &&
            !BusinessProcessConfusionGuards.ShouldBlockOutstandingListing(m))
        {
            ExtractAccount(m, parameters);
            tools.Add("customer.openitems");
            return ("customer.openitems", parameters, tools);
        }

        if (m.Contains("supplier") && m.Contains("list") &&
            !RankingQueryPolicy.IsRankingClassification(classification))
        {
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        if (TryExtractThreshold(message, "valuation", out var minVal) &&
            (m.Contains("inventory") || m.Contains("stock") || m.Contains("item")) &&
            !DeadStockOrSlowMoving(m))
        {
            parameters["minValuation"] = minVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tools.Add("inventoryitem.list");
            return ("inventoryitem.list", parameters, tools);
        }

        if (TryExtractThreshold(message, "balance", out var minSupplierBal) && m.Contains("supplier"))
        {
            parameters["minBalance"] = minSupplierBal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        if (TryExtractThreshold(message, "balance", out var minBalance) && m.Contains("customer") &&
            !BusinessProcessConfusionGuards.ShouldBlockOutstandingListing(m))
        {
            parameters["minBalance"] = minBalance.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tools.Add("customer.list");
            return ("customer.list", parameters, tools);
        }

        if (m.Contains("supplier") && m.Contains("list"))
        {
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        if (m.Contains("customer") && m.Contains("list") && !ChatIntentMatcher.IsCustomerAgedTopQuery(m) &&
            !RankingQueryPolicy.IsRankingClassification(classification) &&
            !BusinessProcessConfusionGuards.ShouldBlockOutstandingListing(m))
        {
            tools.Add("customer.list");
            return ("customer.list", parameters, tools);
        }

        if (m.Contains("customer") && !ChatIntentMatcher.IsCustomerAgedTopQuery(m) &&
            !RankingQueryPolicy.IsRankingClassification(classification) &&
            !BusinessProcessConfusionGuards.ShouldBlockOutstandingListing(m))
        {
            tools.Add("customer.list");
            return ("customer.list", parameters, tools);
        }

        if (m.Contains("supplier") && !RankingQueryPolicy.IsRankingClassification(classification))
        {
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        return (null, parameters, tools);
    }

    private static (string? operation, Dictionary<string, string> parameters, List<string> tools) Finalize(
        string message,
        string? operation,
        Dictionary<string, string> parameters,
        List<string> tools)
    {
        if (operation is null)
            return (null, parameters, tools);

        TagInferredBusinessProcess(operation, tools);

        if (!BusinessProcessConfusionGuards.IsBlocked(message, operation))
            return (operation, parameters, tools);

        if (BusinessProcessConfusionGuards.TryGetCanonicalOverride(message, operation, out var canonical) &&
            !string.IsNullOrEmpty(canonical) && InsightReadOnlyTools.IsAllowed(canonical))
        {
            tools.Add($"guardOverride:{operation}->{canonical}");
            tools.Remove(operation);
            tools.Add(canonical);
            return (canonical, parameters, tools);
        }

        tools.Add($"guardBlocked:{operation}");
        return (null, parameters, tools);
    }

    private static bool DeadStockOrSlowMoving(string m) =>
        m.Contains("dead") || m.Contains("slow") || m.Contains("non moving") || m.Contains("non-moving");

    private static void TagInferredBusinessProcess(string? operation, List<string> tools)
    {
        if (operation is null || tools.Any(t => t.StartsWith("businessProcess:", StringComparison.Ordinal)))
            return;

        var process = operation switch
        {
            _ when operation.Contains("payment.", StringComparison.Ordinal) => BusinessProcessType.PaymentBehavior,
            _ when operation.Contains("reconcile", StringComparison.Ordinal) || operation.Contains(".gl.", StringComparison.Ordinal) =>
                BusinessProcessType.Reconciliation,
            _ when operation.StartsWith("vat.", StringComparison.Ordinal) => BusinessProcessType.VatCompliance,
            _ when operation.StartsWith("treasury.", StringComparison.Ordinal) || operation.StartsWith("bank.", StringComparison.Ordinal) =>
                BusinessProcessType.CashflowIntelligence,
            _ when operation.StartsWith("inventory.", StringComparison.Ordinal) => BusinessProcessType.InventoryLifecycle,
            _ when operation.Contains("discount", StringComparison.Ordinal) => BusinessProcessType.DiscountGovernance,
            _ when operation.Contains("journal.periodend", StringComparison.Ordinal) => BusinessProcessType.MonthEndClose,
            _ => BusinessProcessType.Unknown
        };

        if (process != BusinessProcessType.Unknown)
            tools.Add($"businessProcess:{process}:0.55");
    }

    private static void ExtractAccount(string message, Dictionary<string, string> parameters)
    {
        var match = Regex.Match(message, @"\b([A-Z0-9]{2,12})\b");
        if (match.Success)
            parameters["account"] = match.Groups[1].Value;
    }

    private static bool TryExtractThreshold(string message, string kind, out decimal amount)
    {
        amount = 0;
        var keywordPattern = kind switch
        {
            "valuation" => @"(?:valuation|stock\s*value|inventory\s*value|item\s*value)",
            "balance" => @"(?:balance|owing|outstanding)",
            _ => kind
        };

        var match = Regex.Match(message,
            keywordPattern + @"\s*(?:>|greater\s+than|over|above)\s*([\d][\d,\.]*)\s*(k|m|million|thousand)?",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(message, @">\s*([\d][\d,\.]*)\s*(k|m|million|thousand)?", RegexOptions.IgnoreCase);
            if (!match.Success) return false;
            if (kind == "balance" && !message.Contains("balance", StringComparison.OrdinalIgnoreCase))
                return false;
            if (kind == "valuation" &&
                !message.Contains("valuation", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("stock", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("inventory", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var raw = match.Groups[1].Value.Replace(",", "");
        if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) &&
            !decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.CurrentCulture, out parsed))
            return false;

        var suffix = match.Groups.Count > 2 && match.Groups[2].Success
            ? match.Groups[2].Value.ToLowerInvariant()
            : "";
        amount = suffix switch
        {
            "k" or "thousand" => parsed * 1000,
            "m" or "million" => parsed * 1_000_000,
            _ => parsed
        };
        return true;
    }
}
