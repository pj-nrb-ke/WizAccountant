using System.Text.Json.Serialization;

namespace WizAccountant.Insight.Intents.Tests;

public sealed class AmbiguousIntentCase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("query")]
    public string Query { get; set; } = "";

    [JsonPropertyName("expectedIntent")]
    public string ExpectedIntent { get; set; } = "";

    [JsonPropertyName("expectedSecondaryIntent")]
    public string? ExpectedSecondaryIntent { get; set; }

    [JsonPropertyName("expectedDomain")]
    public string? ExpectedDomain { get; set; }

    [JsonPropertyName("minConfidence")]
    public double MinConfidence { get; set; }

    [JsonPropertyName("preferredOperation")]
    public string? PreferredOperation { get; set; }

    [JsonPropertyName("routeKind")]
    public string? RouteKind { get; set; }

    [JsonPropertyName("maxRows")]
    public int? MaxRows { get; set; }

    [JsonPropertyName("allowAmbiguous")]
    public bool AllowAmbiguous { get; set; }
}
