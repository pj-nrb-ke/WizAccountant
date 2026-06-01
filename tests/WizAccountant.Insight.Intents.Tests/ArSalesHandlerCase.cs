using System.Text.Json.Serialization;

namespace WizAccountant.Insight.Intents.Tests;

public sealed class ArSalesHandlerCase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("query")]
    public string Query { get; set; } = "";

    [JsonPropertyName("operation")]
    public string? Operation { get; set; }

    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("countOnly")]
    public bool CountOnly { get; set; }

    [JsonPropertyName("maxRows")]
    public int? MaxRows { get; set; }

    [JsonPropertyName("forbiddenOperations")]
    public string[]? ForbiddenOperations { get; set; }

    [JsonPropertyName("mustNotRoute")]
    public string[]? MustNotRoute { get; set; }

    [JsonPropertyName("preferredOperation")]
    public string? PreferredOperation { get; set; }
}
