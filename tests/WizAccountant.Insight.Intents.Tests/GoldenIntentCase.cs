using System.Text.Json.Serialization;

namespace WizAccountant.Insight.Intents.Tests;

public sealed class GoldenIntentCase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("query")]
    public string Query { get; set; } = "";

    [JsonPropertyName("expectedIntent")]
    public string ExpectedIntent { get; set; } = "";

    [JsonPropertyName("minConfidence")]
    public double MinConfidence { get; set; }

    [JsonPropertyName("expectedResponseShape")]
    public string? ExpectedResponseShape { get; set; }

    [JsonPropertyName("suppressGrid")]
    public bool SuppressGrid { get; set; }

    [JsonPropertyName("maxRows")]
    public int? MaxRows { get; set; }

    [JsonPropertyName("forbiddenOperations")]
    public string[]? ForbiddenOperations { get; set; }

    [JsonPropertyName("preferredOperation")]
    public string? PreferredOperation { get; set; }

    [JsonPropertyName("useMegaDigestFallback")]
    public bool UseMegaDigestFallback { get; set; }

    [JsonPropertyName("expectedDomain")]
    public string? ExpectedDomain { get; set; }
}
