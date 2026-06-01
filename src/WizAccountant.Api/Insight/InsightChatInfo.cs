namespace WizAccountant.Api.Insight;

/// <summary>Bump when chat routing or replies change — shown in /health and Insight UI so you can confirm the API restarted.</summary>
public static class InsightChatInfo
{
    public const string Version = "2026-06-12-feedback-validation";

    /// <summary>500 catalog entries; dedicated SQL handlers are a small subset (see MegaDigestFallbackMatcher).</summary>
    public const int MegaDigestCatalogSize = 500;
}
