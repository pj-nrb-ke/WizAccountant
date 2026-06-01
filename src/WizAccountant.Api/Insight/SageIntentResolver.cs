namespace WizAccountant.Api.Insight;

/// <summary>
/// Full intent + domain + handler resolution for ambiguous queries (no SQL execution).
/// </summary>
internal static class SageIntentResolver
{
    public enum RouteKind
    {
        None,
        ImplementedHandler,
        MegaDigestFallback,
        Unresolved
    }

    public sealed record Resolution(
        SageIntentEngine.Classification Classification,
        RouteKind Route,
        string? HandlerId,
        string? Operation,
        string? MegaDigestTitle,
        int? MegaDigestId,
        string Summary);

    public static Resolution Resolve(string? message)
    {
        var classification = SageIntentEngine.Classify(message);
        if (string.IsNullOrWhiteSpace(message))
        {
            return new Resolution(classification, RouteKind.None, null, null, null, null,
                "Empty query");
        }

        var m = message.ToLowerInvariant();

        if (TryResolveImplemented(message, m, classification, out var handlerId, out var operation))
        {
            return new Resolution(classification, RouteKind.ImplementedHandler, handlerId, operation,
                null, null, $"Handler: {handlerId} → {operation}");
        }

        var digestMatch = MegaDigestCatalog.Instance.FindBestMatchDetailed(message, minScore: 2);
        if (digestMatch.Entry is not null)
        {
            var resolved = MegaDigestOperationResolver.Resolve(digestMatch.Entry, m);
            if (!resolved.Implemented)
            {
                return new Resolution(classification, RouteKind.MegaDigestFallback, null, resolved.Operation,
                    digestMatch.Entry.Title, digestMatch.Entry.Id,
                    $"Mega digest #{digestMatch.Entry.Id}: {digestMatch.Entry.Title} (SQL not implemented)");
            }
        }

        if (classification.PrimaryIntent != SageIntentEngine.IntentType.Unknown ||
            classification.Domain != SageChatDomain.Layer.Unknown)
        {
            return new Resolution(classification, RouteKind.MegaDigestFallback, null, null,
                digestMatch.Entry?.Title, digestMatch.Entry?.Id,
                $"Intent {classification.PrimaryIntent} / domain {classification.Domain} — use mega digest or domain fallback");
        }

        return new Resolution(classification, RouteKind.Unresolved, null, null, null, null,
            "Unresolved — rephrase with AR/AP/inventory/GL context");
    }

    private static bool TryResolveImplemented(
        string message,
        string m,
        SageIntentEngine.Classification classification,
        out string? handlerId,
        out string? operation)
    {
        handlerId = null;
        operation = null;
        var parameters = new Dictionary<string, string>();
        var tools = new List<string>();

        if (ChatIntentMatcher.TrySalesInvoiceDiscountCount(message, m, parameters, tools, out operation))
        {
            handlerId = "AR-COUNT-DISCOUNTED-INVOICES";
            return true;
        }

        if (m.Contains("credit balance") && !m.Contains("supplier") &&
            !m.Contains("stock") && !m.Contains("inventory") && !m.Contains("balance sheet") &&
            (m.Contains("customer") || m.Contains("debtor") || m.Contains("receivable") || m.Contains("oldest")))
        {
            handlerId = "AR-LIST-CREDIT-BALANCES";
            operation = "customer.credit.balances";
            return true;
        }

        if (ChatIntentMatcher.TryCustomerAgedTop(m, parameters, tools, out operation))
        {
            handlerId = "AR-TOP-AGED-DEBIT";
            return true;
        }

        if (ArPaymentBehaviorChatMatcher.TryRoute(message, m, parameters, tools, out operation))
        {
            var payEntry = HandlerRegistry.Instance.GetByOperation(operation!);
            if (payEntry is not null)
            {
                handlerId = payEntry.Id;
                return true;
            }
        }

        if (ChatIntentMatcher.TryCustomerUnpaidSummary(m, parameters, tools, out operation) ||
            (m.Contains("which") && m.Contains("customer") && m.Contains("highest") &&
             (m.Contains("balance") || m.Contains("outstanding") || m.Contains("unpaid")) &&
             !CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(m)))
        {
            handlerId = "AR-RANK-UNPAID-SUMMARY";
            operation ??= "customer.unpaid.summary";
            return true;
        }

        if (ChatIntentMatcher.TryInventoryBsNegativeLedgers(m, parameters, tools, out operation))
        {
            handlerId = "INV-BS-NEGATIVE-GL";
            return true;
        }

        if (ChatIntentMatcher.TryUnpaidSalesInvoices(m, parameters, tools, out operation))
        {
            handlerId = "AR-LIST-OPEN-ITEMS";
            return true;
        }

        if (ReconciliationChatMatcher.TryRoute(message, m, parameters, tools, out operation))
        {
            var entry = HandlerRegistry.Instance.GetByOperation(operation!);
            if (entry is not null)
            {
                handlerId = entry.Id;
                return true;
            }
        }

        if (GlFinanceChatMatcher.TryRoute(message, m, parameters, tools, out operation))
        {
            var entry = HandlerRegistry.Instance.GetByOperation(operation!);
            if (entry is not null)
            {
                handlerId = entry.Id;
                return true;
            }
        }

        var registry = HandlerRegistry.Instance.FindBest(message, classification);
        if (registry is not null && registry.Implemented)
        {
            // Reversed journals are not implemented — avoid weak "manual" keyword-only matches.
            if (m.Contains("revers") && registry.Operation == "gl.journal.manual")
            {
                handlerId = null;
                operation = null;
            }
            else
            {
                handlerId = registry.Id;
                operation = registry.Operation;
                return true;
            }
        }

        return false;
    }
}
