namespace WizAccountant.Api.Insight;

/// <summary>
/// Back-compat facade over <see cref="SageIntentEngine"/>.
/// </summary>
internal static class SageQueryIntentClassifier
{
    public enum IntentType
    {
        Unknown = SageIntentEngine.IntentType.Unknown,
        Aggregation = SageIntentEngine.IntentType.Aggregation,
        Ranking = SageIntentEngine.IntentType.Ranking,
        Listing = SageIntentEngine.IntentType.Listing,
        Reconciliation = SageIntentEngine.IntentType.Reconciliation,
        Datafix = SageIntentEngine.IntentType.Datafix,
        Audit = SageIntentEngine.IntentType.Audit
    }

    public sealed record Classification(
        IntentType Intent,
        SageChatDomain.Layer Domain,
        string Summary,
        double Confidence = 0,
        IntentType? SecondaryIntent = null,
        double SecondaryConfidence = 0,
        bool IsAmbiguous = false)
    {
        public static Classification FromEngine(SageIntentEngine.Classification c) =>
            new((IntentType)c.PrimaryIntent, c.Domain, c.Summary, c.Confidence,
                c.SecondaryIntent.HasValue ? (IntentType)c.SecondaryIntent.Value : null,
                c.SecondaryConfidence, c.IsAmbiguous);
    }

    public static Classification Classify(string message) =>
        Classification.FromEngine(SageIntentEngine.Classify(message));

    public static bool ShouldUseMegaDigestFallback(Classification c, string message) =>
        SageIntentEngine.ShouldUseMegaDigestFallback(SageIntentEngine.Classify(message));
}
