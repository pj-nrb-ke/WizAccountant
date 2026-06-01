using System.Reflection;
using System.Text.Json;

namespace WizAccountant.Api.Insight;

/// <summary>
/// Central routing registry for implemented Sage read handlers (handler_registry.json).
/// </summary>
internal sealed class HandlerRegistry
{
    private static HandlerRegistry? _instance;
    private static readonly object Lock = new();

    public static HandlerRegistry Instance
    {
        get
        {
            if (_instance is not null) return _instance;
            lock (Lock)
            {
                _instance ??= Load();
                return _instance;
            }
        }
    }

    public string Version { get; }
    public IReadOnlyList<HandlerEntry> Handlers { get; }

    private HandlerRegistry(string version, IReadOnlyList<HandlerEntry> handlers)
    {
        Version = version;
        Handlers = handlers;
    }

    public HandlerEntry? FindBest(string message, SageIntentEngine.Classification classification)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var m = message.ToLowerInvariant();
        var tokens = Tokenize(m);
        HandlerEntry? best = null;
        var bestScore = 0.0;

        foreach (var h in Handlers.Where(x => x.Implemented))
        {
            if (!IntentMatches(h.Intent, classification.PrimaryIntent))
                continue;

            var keywordHits = h.Keywords.Count(k => m.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (keywordHits == 0)
                continue;

            var tokenOverlap = tokens.Intersect(Tokenize(string.Join(" ", h.Keywords))).Count();
            var score = keywordHits * 15 + tokenOverlap * 5;
            if (DomainMatches(h.Domain, classification.Domain))
                score += 10;

            if (score > bestScore)
            {
                bestScore = score;
                best = h;
            }
        }

        return best;
    }

    public HandlerEntry? GetByOperation(string operation) =>
        Handlers.FirstOrDefault(h =>
            string.Equals(h.Operation, operation, StringComparison.OrdinalIgnoreCase));

    public HandlerEntry? GetCanonical(string operation) =>
        Handlers.FirstOrDefault(h =>
            h.IsCanonical && string.Equals(h.Operation, operation, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<HandlerEntry> GetByIntent(SageIntentEngine.IntentType intent)
    {
        var name = IntentToRegistryName(intent);
        return Handlers.Where(h => string.Equals(h.Intent, name, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static bool IntentMatches(string registryIntent, SageIntentEngine.IntentType classificationIntent)
    {
        var expected = IntentToRegistryName(classificationIntent);
        return string.Equals(registryIntent, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string IntentToRegistryName(SageIntentEngine.IntentType intent) => intent switch
    {
        SageIntentEngine.IntentType.Aggregation => "aggregation",
        SageIntentEngine.IntentType.Ranking => "ranking",
        SageIntentEngine.IntentType.Listing => "listing",
        SageIntentEngine.IntentType.Reconciliation => "reconciliation",
        SageIntentEngine.IntentType.Datafix => "datafix",
        SageIntentEngine.IntentType.Audit => "audit",
        _ => "unknown"
    };

    private static bool DomainMatches(string registryDomain, SageChatDomain.Layer layer)
    {
        var d = registryDomain.ToUpperInvariant();
        return layer switch
        {
            SageChatDomain.Layer.AccountsReceivable => d is "AR",
            SageChatDomain.Layer.AccountsPayable => d is "AP",
            SageChatDomain.Layer.Inventory => d.Contains("INV"),
            SageChatDomain.Layer.GeneralLedger => d is "GL",
            SageChatDomain.Layer.Dashboard => d.Contains("DASH"),
            _ => false
        };
    }

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in text.Split(
                     new[] { ' ', ',', '.', '?', '!', ';', ':', '-', '(', ')', '"', '\r', '\n', '\t' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length >= 3)
                tokens.Add(part);
        }

        return tokens;
    }

    private static HandlerRegistry Load()
    {
        const string resourceName = "WizAccountant.Api.Insight.Data.handler_registry.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";

        var list = new List<HandlerEntry>();
        if (root.TryGetProperty("handlers", out var handlers) && handlers.ValueKind == JsonValueKind.Array)
        {
            foreach (var h in handlers.EnumerateArray())
            {
                var id = h.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                var domain = h.TryGetProperty("domain", out var d) ? d.GetString() ?? "" : "";
                var intent = h.TryGetProperty("intent", out var i) ? i.GetString() ?? "" : "";
                var operation = h.TryGetProperty("operation", out var o) ? o.GetString() ?? "" : "";
                var implemented = h.TryGetProperty("implemented", out var impl) && impl.GetBoolean();
                var businessProcess = h.TryGetProperty("business_process", out var bp) ? bp.GetString() : null;
                var canonicalMeaning = h.TryGetProperty("canonical_meaning", out var cm) ? cm.GetString() : null;
                var isCanonical = h.TryGetProperty("is_canonical", out var ic) && ic.GetBoolean();
                var explainability = h.TryGetProperty("explainability", out var ex) && ex.GetBoolean();
                var reconciliation = h.TryGetProperty("reconciliation", out var rc) && rc.GetBoolean();
                var drilldown = h.TryGetProperty("drilldown", out var dd) && dd.GetBoolean();
                var keywords = new List<string>();
                if (h.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array)
                {
                    foreach (var k in kw.EnumerateArray())
                    {
                        var s = k.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            keywords.Add(s);
                    }
                }

                if (!string.IsNullOrEmpty(id))
                {
                    list.Add(new HandlerEntry(
                        id, domain, intent, operation, implemented, keywords,
                        businessProcess, canonicalMeaning, isCanonical, explainability, reconciliation, drilldown));
                }
            }
        }

        return new HandlerRegistry(version, list);
    }
}

internal sealed record HandlerEntry(
    string Id,
    string Domain,
    string Intent,
    string Operation,
    bool Implemented,
    IReadOnlyList<string> Keywords,
    string? BusinessProcess = null,
    string? CanonicalMeaning = null,
    bool IsCanonical = false,
    bool Explainability = false,
    bool Reconciliation = false,
    bool Drilldown = false);
