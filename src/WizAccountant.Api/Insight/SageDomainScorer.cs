namespace WizAccountant.Api.Insight;

/// <summary>Weighted domain signals for ambiguous Sage business queries (AR, AP, Inventory, GL, Audit).</summary>
internal static class SageDomainScorer
{
    public sealed record DomainSignal(SageChatDomain.Layer Layer, double Score, string Reason);

    public static (SageChatDomain.Layer Primary, double Confidence, IReadOnlyList<DomainSignal> Signals) Score(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return (SageChatDomain.Layer.Unknown, 0, Array.Empty<DomainSignal>());

        var signals = new List<DomainSignal>();
        ScoreAudit(messageLower, signals);
        ScoreInventory(messageLower, signals);
        ScoreAr(messageLower, signals);
        ScoreAp(messageLower, signals);
        ScoreGl(messageLower, signals);
        ScoreDashboard(messageLower, signals);
        ScoreManufacturing(messageLower, signals);
        ScoreFixedAssets(messageLower, signals);

        var ordered = signals.OrderByDescending(s => s.Score).ToList();
        var top = ordered.FirstOrDefault();
        var confidence = top?.Score ?? 0;

        if (ordered.Count > 1 && ordered[1].Score >= confidence * 0.8)
            confidence = Math.Round(confidence * 0.88, 3);

        return (top?.Layer ?? SageChatDomain.Layer.Unknown, Math.Clamp(confidence, 0, 1), ordered);
    }

    private static void Add(List<DomainSignal> signals, SageChatDomain.Layer layer, double score, string reason)
    {
        if (score <= 0) return;
        var existing = signals.FirstOrDefault(s => s.Layer == layer);
        if (existing is not null)
        {
            signals.Remove(existing);
            score = Math.Max(score, existing.Score);
            reason = $"{existing.Reason}; {reason}";
        }

        signals.Add(new DomainSignal(layer, Math.Min(0.98, score), reason));
    }

    private static void ScoreAr(string m, List<DomainSignal> signals)
    {
        var score = 0.0;
        if (m.Contains("customer") || m.Contains("debtor") || m.Contains("receivable"))
            score = Math.Max(score, 0.72);
        if (m.Contains("invoice") && !m.Contains("supplier") && !m.Contains("purchase"))
            score = Math.Max(score, 0.68);
        if (m.Contains("credit limit") || m.Contains("aged balance") || m.Contains("aging"))
            score = Math.Max(score, 0.8);
        if (m.Contains("oldest") && m.Contains("debit"))
            score = Math.Max(score, 0.85);
        if (m.Contains("credit balance") && !m.Contains("supplier"))
            score = Math.Max(score, 0.82);
        if (m.Contains(" ar ") || m.StartsWith("ar ") || m.EndsWith(" ar"))
            score = Math.Max(score, 0.75);
        if (m.Contains("sales") || m.Contains("revenue"))
            score = Math.Max(score, 0.65);

        if (score > 0)
            Add(signals, SageChatDomain.Layer.AccountsReceivable, score, "AR signals (customer/debtor/invoice/aging)");
    }

    private static void ScoreAp(string m, List<DomainSignal> signals)
    {
        var score = 0.0;
        if (m.Contains("supplier") || m.Contains("creditor") || m.Contains("payable"))
            score = Math.Max(score, 0.75);
        if (m.Contains("bill") || m.Contains("payment") && m.Contains("supplier"))
            score = Math.Max(score, 0.7);
        if (m.Contains("purchase invoice") || m.Contains("vendor"))
            score = Math.Max(score, 0.72);
        if (m.Contains("oldest") && m.Contains("credit") && m.Contains("supplier"))
            score = Math.Max(score, 0.85);

        if (score > 0)
            Add(signals, SageChatDomain.Layer.AccountsPayable, score, "AP signals (supplier/creditor/bills/payments)");
    }

    private static void ScoreInventory(string m, List<DomainSignal> signals)
    {
        var score = 0.0;

        if (m.Contains("fix") && (m.Contains("inventory") || m.Contains("stock") || m.Contains("gl")))
            score = Math.Max(score, 0.85);

        if (ChatIntentMatcher.IsInventoryBsNegativeLedgersQuery(m) ||
            (m.Contains("negative") && m.Contains("balance sheet") && (m.Contains("stock") || m.Contains("inventory"))))
            score = Math.Max(score, 0.92);

        if (m.Contains("warehouse") || m.Contains("stock item") || m.Contains("stkitem"))
            score = Math.Max(score, 0.78);
        if (m.Contains("valuation") || m.Contains("stock value") || m.Contains("inventory value"))
            score = Math.Max(score, 0.8);
        if (m.Contains("balance sheet") && (m.Contains("stock") || m.Contains("inventory")))
            score = Math.Max(score, 0.85);
        if (m.Contains("on hand") || m.Contains("on-hand") || m.Contains("slow moving"))
            score = Math.Max(score, 0.7);

        if (score > 0)
            Add(signals, SageChatDomain.Layer.Inventory, score, "Inventory signals (stock/valuation/warehouse/BS stock)");
    }

    private static void ScoreGl(string m, List<DomainSignal> signals)
    {
        var score = 0.0;
        if (m.Contains("trial balance") || m.Contains("general ledger") || m.Contains("postgl"))
            score = Math.Max(score, 0.85);
        if (m.Contains("journal") && !m.Contains("duplicate"))
            score = Math.Max(score, 0.72);
        if (m.Contains("ledger") && !m.Contains("stock"))
            score = Math.Max(score, 0.68);
        if (m.Contains("display") && m.Contains("gl"))
            score = Math.Max(score, 0.7);
        if (m.Contains("balance sheet") && !m.Contains("stock") && !m.Contains("inventory"))
            score = Math.Max(score, 0.75);

        if (score > 0)
            Add(signals, SageChatDomain.Layer.GeneralLedger, score, "GL signals (ledger/journal/trial balance)");
    }

    private static void ScoreAudit(string m, List<DomainSignal> signals)
    {
        var score = 0.0;
        if (m.Contains("duplicate"))
            score = Math.Max(score, 0.8);
        if (m.Contains("suspicious") || m.Contains("fraud"))
            score = Math.Max(score, 0.82);
        if (m.Contains("reversed") || m.Contains("backdated") || m.Contains("back-dated"))
            score = Math.Max(score, 0.78);
        if (m.Contains("manual") && (m.Contains("post") || m.Contains("journal")))
            score = Math.Max(score, 0.75);
        if (m.Contains("audit"))
            score = Math.Max(score, 0.88);

        if (score > 0)
            Add(signals, SageChatDomain.Layer.GeneralLedger, score * 0.85, "Audit often drills into GL");
        // Audit is cross-cutting — also bump via intent, not a separate Layer enum value
    }

    private static void ScoreDashboard(string m, List<DomainSignal> signals)
    {
        if (m.Contains("dashboard") || m.Contains("kpi"))
            Add(signals, SageChatDomain.Layer.Dashboard, 0.85, "Dashboard/KPI");
    }

    private static void ScoreManufacturing(string m, List<DomainSignal> signals)
    {
        if (m.Contains("bom") || m.Contains("manufactur") || m.Contains("wip"))
            Add(signals, SageChatDomain.Layer.Manufacturing, 0.8, "Manufacturing/BOM/WIP");
    }

    private static void ScoreFixedAssets(string m, List<DomainSignal> signals)
    {
        if (m.Contains("fixed asset") || m.Contains("depreciation") || m.Contains("nbv"))
            Add(signals, SageChatDomain.Layer.FixedAssets, 0.82, "Fixed assets");
    }
}
