namespace WizAccountant.Api.Insight;

internal enum MegaDigestMatchKind
{
    None,
    ExactPhrase,
    KeywordOverlap,
    SemanticSimilarity,
    DomainFallback
}

internal sealed record MegaDigestMatchResult(
    MegaDigestEntry? Entry,
    MegaDigestMatchKind Kind,
    double Confidence,
    string Detail);
