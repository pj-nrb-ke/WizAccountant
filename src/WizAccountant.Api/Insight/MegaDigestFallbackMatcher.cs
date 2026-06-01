namespace WizAccountant.Api.Insight;

/// <summary>
/// When no dedicated SQL handler exists, return catalog-matched business intent (never generic help only).
/// </summary>
internal static class MegaDigestFallbackMatcher
{
    public static bool TryBuildReply(
        string message,
        SageIntentEngine.Classification classification,
        out string reply,
        out List<string> citations)
    {
        reply = "";
        citations = new List<string>();
        var messageLower = message.ToLowerInvariant();

        var match = MegaDigestCatalog.Instance.FindBestMatchDetailed(message, minScore: 2);
        if (match.Entry is not null && match.Kind != MegaDigestMatchKind.None)
        {
            var resolved = MegaDigestOperationResolver.Resolve(match.Entry, messageLower);
            if (resolved.Implemented && !string.IsNullOrEmpty(resolved.Operation))
                return false;

            reply = FormatCatalogFallback(match.Entry, resolved, classification, match);
            citations.Add($"mega-digest:{match.Entry.Id}");
            citations.Add($"match:{match.Kind}");
            if (classification.Confidence > 0)
                citations.Add($"intent:{classification.PrimaryIntent}:{classification.Confidence:F2}");
            return true;
        }

        if (TryDomainFallback(messageLower, classification, out reply))
        {
            citations.Add("mega-digest:domain");
            citations.Add($"intent:{classification.PrimaryIntent}");
            return true;
        }

        if (TryIntentOnlyFallback(classification, message, out reply))
        {
            citations.Add("mega-digest:intent-only");
            return true;
        }

        reply = "";
        return false;
    }

    public static MegaDigestCoverageSummary GetCoverageSummary()
    {
        var catalog = MegaDigestCatalog.Instance;
        var withHandler = 0;
        var dedicated = 0;

        foreach (var entry in catalog.Entries)
        {
            var resolved = MegaDigestOperationResolver.Resolve(entry, "");
            if (!string.IsNullOrEmpty(resolved.Operation))
                withHandler++;
            if (resolved.Implemented)
                dedicated++;
        }

        return new MegaDigestCoverageSummary(
            catalog.Entries.Count,
            dedicated,
            withHandler,
            catalog.Entries.Count - dedicated);
    }

    private static string FormatCatalogFallback(
        MegaDigestEntry entry,
        MegaDigestOperationResolver.ResolveResult resolved,
        SageIntentEngine.Classification classification,
        MegaDigestMatchResult match)
    {
        var lines = new List<string>
        {
            "Finding:",
            $"I understand this as: {entry.Title}",
            "",
            "This query is recognized in the Sage AI Agent training catalog (500-query mega digest), but the dedicated SQL handler is not implemented yet.",
            "",
            $"Recognized business intent: {entry.Title}",
            $"Domain: {entry.Domain}",
            $"Detected query type: {classification.PrimaryIntent} (confidence {classification.Confidence:P0})",
            $"Catalog match: {match.Kind} ({match.Detail}, score {match.Confidence:P0})",
            "Status: SQL handler not yet implemented",
        };

        if (!string.IsNullOrEmpty(resolved.Operation))
        {
            lines.Add($"Closest supported operation (proxy, not run): {resolved.Operation}");
            lines.Add("Note: Insight will not run a partial proxy that could dump hundreds of master rows for this question.");
        }

        var registry = HandlerRegistry.Instance.GetByIntent(classification.PrimaryIntent)
            .Where(h => h.Implemented)
            .Take(3)
            .Select(h => h.Id)
            .ToList();
        if (registry.Count > 0)
        {
            lines.Add("");
            lines.Add("Implemented handlers for this query type in registry:");
            lines.Add(string.Join(", ", registry));
        }

        lines.Add("");
        lines.Add("Suggested next action:");
        lines.Add($"Implement the SQL handler for mega digest item #{entry.Id} ({entry.Title}).");

        if (!string.IsNullOrEmpty(resolved.DigestNote))
            lines.Add($"\nTechnical note: {resolved.DigestNote}");

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryDomainFallback(
        string messageLower,
        SageIntentEngine.Classification classification,
        out string reply)
    {
        reply = "";
        var layer = classification.Domain != SageChatDomain.Layer.Unknown
            ? classification.Domain
            : SageChatDomain.Detect(messageLower);

        if (layer == SageChatDomain.Layer.Unknown)
            return false;

        var domain = layer switch
        {
            SageChatDomain.Layer.AccountsReceivable => "Accounts Receivable (AR)",
            SageChatDomain.Layer.AccountsPayable => "Accounts Payable (AP)",
            SageChatDomain.Layer.Inventory => "Inventory / Stock",
            SageChatDomain.Layer.GeneralLedger => "General Ledger (GL)",
            SageChatDomain.Layer.FixedAssets => "Fixed Assets",
            SageChatDomain.Layer.Manufacturing => "Manufacturing / BOM",
            SageChatDomain.Layer.Dashboard => "Dashboard / KPIs",
            _ => layer.ToString()
        };

        var examples = layer switch
        {
            SageChatDomain.Layer.AccountsReceivable =>
                "Implemented today: top aged debitors, credit balances, highest outstanding, discounted invoice count, open/unpaid invoices.",
            SageChatDomain.Layer.AccountsPayable =>
                "Implemented today: oldest supplier balances (aged top). Other AP analytics are catalog-only until SQL is added.",
            SageChatDomain.Layer.Inventory =>
                "Implemented today: inventory vs balance sheet reconcile, negative stock GL on balance sheet, item valuation list.",
            SageChatDomain.Layer.GeneralLedger =>
                "Partial: sample GL transactions. Most GL audit queries need dedicated SQL.",
            SageChatDomain.Layer.FixedAssets =>
                "Lower priority — FA depreciation batch vs PostGL not in chat yet.",
            SageChatDomain.Layer.Manufacturing =>
                "Lower priority — BOM/WIP/PostST analytics not in chat yet.",
            SageChatDomain.Layer.Dashboard =>
                "Implemented: dashboard.summary KPI counts.",
            _ => "See DOCS/Sage_AI_Agent_500_Common_Business_Queries_Mega_Digest.md."
        };

        reply = string.Join(Environment.NewLine, new[]
        {
            "Finding:",
            "Your question maps to a Sage domain, but no close mega-digest catalog title matched.",
            "",
            $"Recognized domain: {domain}",
            $"Detected query type: {classification.PrimaryIntent} (confidence {classification.Confidence:P0})",
            "Status: SQL handler not yet implemented for this wording",
            "",
            "Implemented in this domain:",
            examples,
            "",
            "Suggested next action:",
            "Rephrase using a catalog title from DOCS/Sage_AI_Agent_500_Common_Business_Queries_Mega_Digest.md, or ask to add SQL for this domain."
        });

        return true;
    }

    private static bool TryIntentOnlyFallback(
        SageIntentEngine.Classification classification,
        string message,
        out string reply)
    {
        reply = "";
        if (classification.PrimaryIntent == SageIntentEngine.IntentType.Unknown ||
            classification.Confidence < 0.4)
            return false;

        var shape = classification.ExpectedResponse switch
        {
            SageIntentEngine.ResponseShape.SingleNumber => "a single numeric answer (COUNT/SUM), not transaction rows",
            SageIntentEngine.ResponseShape.TopRows => $"a ranked TOP {RankingQueryPolicy.DefaultTop} result, not a full master list",
            SageIntentEngine.ResponseShape.ReconciliationReport => "a reconciliation comparison with variances",
            SageIntentEngine.ResponseShape.DiagnosticPreview => "a diagnostic preview (no auto-posting)",
            SageIntentEngine.ResponseShape.RowList => "a filtered row listing",
            _ => "a structured Sage read response"
        };

        var registryHints = HandlerRegistry.Instance.GetByIntent(classification.PrimaryIntent)
            .Where(h => h.Implemented)
            .Select(h => $"{h.Id} → {h.Operation}")
            .Take(4);

        reply = string.Join(Environment.NewLine, new[]
        {
            "Finding:",
            $"Recognized query type: **{classification.PrimaryIntent}** (confidence {classification.Confidence:P0}).",
            $"Expected response shape: {shape}.",
            "",
            "Status: No dedicated SQL handler matched this exact wording yet.",
            "",
            "Implemented handlers for this intent:",
            registryHints.Any() ? string.Join(Environment.NewLine, registryHints) : "(none registered — extend handler_registry.json)",
            "",
            "Suggested next action:",
            "Rephrase closer to a mega-digest catalog title, or implement SQL for this intent in the connector.",
            "",
            $"Your message: \"{Truncate(message, 120)}\""
        });

        return true;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

internal sealed record MegaDigestCoverageSummary(
    int CatalogEntries,
    int DedicatedHandlers,
    int WithProxyMapping,
    int PendingSql);
