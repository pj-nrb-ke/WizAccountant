using System.Text.RegularExpressions;

namespace WizAccountant.Api.Insight;

/// <summary>
/// Centralized intent classification (first stage before SQL/handler selection).
/// DOCS/Sage_AI_Training/02_Intent_Classification_Rules.md
/// </summary>
internal static class SageIntentEngine
{
    public enum IntentType
    {
        Unknown,
        Aggregation,
        Ranking,
        Listing,
        Reconciliation,
        Datafix,
        Audit
    }

    public enum ResponseShape
    {
        Unknown,
        SingleNumber,
        TopRows,
        RowList,
        ReconciliationReport,
        DiagnosticPreview,
        Educational
    }

    public sealed record IntentCandidate(IntentType Intent, double Score, string Reason);

    public sealed record Classification(
        IntentType PrimaryIntent,
        double Confidence,
        IntentType? SecondaryIntent,
        double SecondaryConfidence,
        SageChatDomain.Layer Domain,
        double DomainConfidence,
        ResponseShape ExpectedResponse,
        IReadOnlyList<IntentCandidate> Candidates,
        IReadOnlyList<SageDomainScorer.DomainSignal> DomainSignals,
        string Summary,
        bool IsAmbiguous);

    public static Classification Classify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Empty("Empty query");
        }

        var m = message.ToLowerInvariant();
        var (domain, domainConfidence, domainSignals) = SageDomainScorer.Score(m);
        var candidates = new List<IntentCandidate>();

        ScoreDatafix(m, candidates);
        ScoreReconciliation(m, candidates);
        ScoreInventoryBsListing(m, candidates);
        ScoreAggregation(message, m, candidates);
        ScoreRanking(m, candidates);
        ScoreWhichIntent(m, candidates);
        ScoreAudit(m, candidates);
        ScoreListing(m, candidates);

        MergeOrAdjustCandidates(m, candidates);

        var ordered = candidates.OrderByDescending(c => c.Score).ToList();
        var primary = ordered.FirstOrDefault();
        var intent = primary?.Intent ?? IntentType.Unknown;
        var confidence = primary?.Score ?? 0;

        IntentType? secondary = null;
        var secondaryConfidence = 0.0;
        var ambiguous = false;

        if (ordered.Count > 1)
        {
            var runnerUp = ordered[1];
            if (runnerUp.Score >= confidence * 0.75)
            {
                ambiguous = true;
                secondary = runnerUp.Intent;
                secondaryConfidence = runnerUp.Score;
                confidence = Math.Round(confidence * 0.9, 3);
            }
        }

        confidence = Math.Clamp(confidence, 0, 1);
        secondaryConfidence = Math.Clamp(secondaryConfidence, 0, 1);

        if (domain == SageChatDomain.Layer.Unknown)
            domain = SageChatDomain.Detect(m);

        var response = MapResponseShape(intent);
        var summary = BuildSummary(intent, secondary, domain, ambiguous, primary?.Reason);

        return new Classification(
            intent, confidence, secondary, secondaryConfidence,
            domain, domainConfidence, response, ordered, domainSignals, summary, ambiguous);
    }

    public static bool ShouldUseMegaDigestFallback(Classification c) =>
        c.PrimaryIntent == IntentType.Unknown ||
        (c.PrimaryIntent == IntentType.Audit && c.Confidence < 0.55);

    public static bool IsHighConfidence(Classification c, double threshold = 0.55) =>
        c.Confidence >= threshold;

    private static Classification Empty(string reason) =>
        new(IntentType.Unknown, 0, null, 0, SageChatDomain.Layer.Unknown, 0,
            ResponseShape.Unknown, Array.Empty<IntentCandidate>(), Array.Empty<SageDomainScorer.DomainSignal>(),
            reason, false);

    private static string BuildSummary(
        IntentType primary,
        IntentType? secondary,
        SageChatDomain.Layer domain,
        bool ambiguous,
        string? primaryReason)
    {
        var baseSummary = primaryReason ?? "Unclassified";
        if (domain != SageChatDomain.Layer.Unknown)
            baseSummary += $" | domain: {domain}";
        if (ambiguous && secondary.HasValue)
            baseSummary += $" | also: {secondary}";
        return baseSummary;
    }

    private static void MergeOrAdjustCandidates(string m, List<IntentCandidate> candidates)
    {
        if (m.Contains("how many") && HasBusinessEntity(m))
        {
            Demote(candidates, IntentType.Ranking, 0.5);
        }

        if (m.Contains("most ") && m.Contains("how many"))
        {
            Demote(candidates, IntentType.Ranking, 0.4);
        }
    }

    private static void Demote(List<IntentCandidate> candidates, IntentType intent, double factor)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Intent == intent)
                candidates[i] = candidates[i] with { Score = candidates[i].Score * factor };
        }
    }

    private static bool HasBusinessEntity(string m) =>
        m.Contains("customer") || m.Contains("supplier") || m.Contains("invoice") ||
        m.Contains("item") || m.Contains("stock") || m.Contains("debtor") ||
        m.Contains("creditor") || m.Contains("vendor") || m.Contains("product") ||
        m.Contains("warehouse") || m.Contains("discounted") || m.Contains("sales");

    private static bool IsHowMuchAggregation(string m) =>
        m.Contains("how much") &&
        !QueryAggregationMode.WantsExplicitRowListing(m) &&
        (m.Contains("total") || m.Contains("sum") || m.Contains("outstanding") ||
         m.Contains("owed") || m.Contains("owing") || m.Contains("balance") ||
         m.Contains("invoice") || m.Contains("sales") || m.Contains("revenue") ||
         m.Contains("stock") || m.Contains("valuation") || HasBusinessEntity(m));

    private static ResponseShape MapResponseShape(IntentType intent) => intent switch
    {
        IntentType.Aggregation => ResponseShape.SingleNumber,
        IntentType.Ranking => ResponseShape.TopRows,
        IntentType.Listing => ResponseShape.RowList,
        IntentType.Reconciliation => ResponseShape.ReconciliationReport,
        IntentType.Datafix => ResponseShape.DiagnosticPreview,
        IntentType.Audit => ResponseShape.RowList,
        _ => ResponseShape.Unknown
    };

    private static void ScoreInventoryBsListing(string m, List<IntentCandidate> candidates)
    {
        if (!ChatIntentMatcher.IsInventoryBsNegativeLedgersQuery(m))
            return;

        candidates.Add(new(IntentType.Listing, 0.9,
            "Inventory GL credit balances on balance sheet (not qty on hand)"));
    }

    private static void ScoreDatafix(string m, List<IntentCandidate> candidates)
    {
        var score = 0.0;
        if (InventoryFixWorkflow.IsFixWorkflowRequest(m))
            score = 0.95;
        else if (m.Contains("fix") && (m.Contains("reconcil") || m.Contains("match") || m.Contains("inventory")))
            score = 0.82;
        else if (m.Contains("correct") && m.Contains("mismatch"))
            score = 0.7;

        if (score > 0)
            candidates.Add(new(IntentType.Datafix, score, "Datafix / diagnostic preview workflow"));
    }

    private static void ScoreReconciliation(string m, List<IntentCandidate> candidates)
    {
        var score = 0.0;
        if (SageChatDomain.IsInventoryGlReconciliationQuestion(m))
            score = 0.92;
        else if ((m.Contains("reconcil") || m.Contains("variance") || m.Contains("not matching") ||
                  m.Contains("does not match") || m.Contains("doesn't match")) &&
                 (m.Contains("match") || m.Contains("gl") || m.Contains("balance sheet") ||
                  m.Contains("valuation") || m.Contains("inventory")))
            score = 0.78;
        else if (m.Contains("matching") && (m.Contains("balance sheet") || m.Contains("gl")))
            score = 0.72;

        if (score > 0)
            candidates.Add(new(IntentType.Reconciliation, score, "Reconciliation / variance"));
    }

    private static void ScoreAggregation(string message, string m, List<IntentCandidate> candidates)
    {
        var isAgg = QueryAggregationMode.IsAggregationQuery(message) || IsHowMuchAggregation(m);
        if (!isAgg)
            return;

        var score = 0.75;
        if (m.Contains("how many") || m.Contains("number of"))
            score = 0.92;
        else if (m.Contains("how much"))
            score = 0.88;
        else if (m.Contains("count of") || m.Contains("count the"))
            score = 0.88;
        else if (m.Contains("total count"))
            score = 0.9;

        if (m.Contains("most ") && HasBusinessEntity(m) && !m.Contains("how many"))
            score = Math.Min(score, 0.45);

        if (QueryAggregationMode.WantsExplicitRowListing(m))
            score *= 0.35;

        candidates.Add(new(IntentType.Aggregation, score, "Aggregation (count/SUM/how much)"));
    }

    private static void ScoreRanking(string m, List<IntentCandidate> candidates)
    {
        var score = 0.0;

        if (m.Contains("top ") || Regex.IsMatch(m, @"\btop\s*\d+"))
            score = Math.Max(score, 0.88);

        if (m.Contains("highest") || m.Contains("lowest") || m.Contains("biggest") || m.Contains("largest"))
            score = Math.Max(score, 0.8);

        if (m.Contains("oldest") || m.Contains("newest"))
            score = Math.Max(score, 0.85);

        if (m.Contains("oldest") && m.Contains("credit") && m.Contains("balance"))
            score = Math.Max(score, 0.9);

        if ((Regex.IsMatch(m, @"\bmost\b") || m.StartsWith("most ")) && HasBusinessEntity(m) && !m.Contains("how many"))
            score = Math.Max(score, 0.84);

        if ((m.Contains("overdue") || m.Contains("aged")) && (m.Contains("debtor") || m.Contains("customer") || m.Contains("invoice")))
            score = Math.Max(score, 0.78);

        if (m.Contains("credit limit") && (m.Contains("aged") || m.Contains("overdue") || m.Contains("balance")))
            score = Math.Max(score, 0.72);

        if (m.Contains("which customer") || m.Contains("which supplier"))
            score = Math.Max(score, 0.76);

        if (m.Contains("which ") && (m.Contains("highest") || m.Contains("most") || m.Contains("oldest")))
            score = Math.Max(score, 0.82);

        if (score > 0 && (QueryAggregationMode.IsAggregationQuery(m) || IsHowMuchAggregation(m)))
            score *= 0.4;

        if (score > 0)
            candidates.Add(new(IntentType.Ranking, score, "Ranking (top N / most / oldest / which leader)"));
    }

    private static void ScoreWhichIntent(string m, List<IntentCandidate> candidates)
    {
        if (!m.Contains("which "))
            return;

        if (m.Contains("highest") || m.Contains("most") || m.Contains("oldest") || m.Contains("largest") ||
            m.Contains("lowest") || m.Contains("biggest"))
            return;

        if (QueryAggregationMode.WantsExplicitRowListing(m) ||
            m.Contains("list") || m.Contains("show") || m.Contains("display"))
        {
            candidates.Add(new(IntentType.Listing, 0.72, "Which — explicit list/show context"));
            return;
        }

        if (HasBusinessEntity(m))
            candidates.Add(new(IntentType.Ranking, 0.68, "Which — pick one leader (ranking)"));
    }

    private static void ScoreAudit(string m, List<IntentCandidate> candidates)
    {
        var score = 0.0;
        if (m.Contains("audit"))
            score = 0.85;
        else if (m.Contains("fraud") || m.Contains("suspicious"))
            score = 0.82;
        else if (m.Contains("duplicate"))
            score = 0.78;
        else if (m.Contains("reversed") || m.Contains("backdated") || m.Contains("back-dated"))
            score = 0.8;
        else if (m.Contains("manual") && (m.Contains("post") || m.Contains("journal")))
            score = 0.75;

        if (score > 0)
            candidates.Add(new(IntentType.Audit, score, "Audit / investigation"));
    }

    private static void ScoreListing(string m, List<IntentCandidate> candidates)
    {
        var score = 0.0;

        if (QueryAggregationMode.WantsExplicitRowListing(m) ||
            m.Contains("list ") || m.StartsWith("list ") ||
            m.Contains("show me") || m.Contains("display"))
            score = Math.Max(score, 0.72);

        if (m.Contains("trial balance") || m.Contains("journal ent"))
            score = Math.Max(score, 0.7);

        if (m.Contains("warehouse") && (m.Contains("stock") || m.Contains("item") || m.Contains("valuation")))
            score = Math.Max(score, 0.68);

        if ((m.Contains("creditor") || m.Contains("supplier")) && m.Contains("payment"))
            score = Math.Max(score, 0.65);

        if (score <= 0)
            return;

        if (ChatIntentMatcher.IsInventoryBsNegativeLedgersQuery(m))
            score = Math.Min(score, 0.55);

        candidates.Add(new(IntentType.Listing, score, "Listing / filtered rows"));
    }
}
