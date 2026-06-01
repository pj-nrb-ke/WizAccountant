using System.Reflection;
using System.Text.Json;

namespace WizAccountant.Api.Insight;

/// <summary>500-query training digest (DOCS/Sage_AI_Agent_500_Common_Business_Queries_Mega_Digest.md).</summary>
internal sealed class MegaDigestCatalog
{
    private static MegaDigestCatalog? _instance;
    private static readonly object Lock = new();

    public static MegaDigestCatalog Instance
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

    public IReadOnlyList<MegaDigestEntry> Entries { get; }
    public string Source { get; }

    private MegaDigestCatalog(IReadOnlyList<MegaDigestEntry> entries, string source)
    {
        Entries = entries;
        Source = source;
    }

    public MegaDigestEntry? FindBestMatch(string userMessage, int minScore = 3) =>
        FindBestMatchDetailed(userMessage, minScore).Entry;

    /// <summary>Multi-tier match: exact phrase → keyword overlap → semantic (Jaccard) similarity.</summary>
    public MegaDigestMatchResult FindBestMatchDetailed(string userMessage, int minScore = 2)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return new MegaDigestMatchResult(null, MegaDigestMatchKind.None, 0, "Empty message");

        var normalizedMsg = NormalizePhrase(userMessage);
        var msgTokens = Tokenize(userMessage);
        if (msgTokens.Count == 0)
            return new MegaDigestMatchResult(null, MegaDigestMatchKind.None, 0, "No tokens");

        MegaDigestEntry? best = null;
        MegaDigestMatchKind bestKind = MegaDigestMatchKind.None;
        var bestConfidence = 0.0;
        var bestDetail = "";

        foreach (var entry in Entries)
        {
            var normalizedTitle = NormalizePhrase(entry.Title);

            if (normalizedMsg.Contains(normalizedTitle, StringComparison.Ordinal) ||
                normalizedTitle.Contains(normalizedMsg, StringComparison.Ordinal))
            {
                var conf = 0.95;
                if (conf > bestConfidence)
                {
                    best = entry;
                    bestKind = MegaDigestMatchKind.ExactPhrase;
                    bestConfidence = conf;
                    bestDetail = "Exact or substring phrase match";
                }

                continue;
            }

            var titleTokens = Tokenize(entry.Title);
            if (titleTokens.Count == 0)
                continue;

            var overlap = msgTokens.Intersect(titleTokens).Count();
            if (overlap >= minScore)
            {
                var keywordConf = Math.Min(0.9, 0.35 + overlap * 0.12 + (overlap * 1.0 / titleTokens.Count) * 0.25);
                if (keywordConf > bestConfidence)
                {
                    best = entry;
                    bestKind = MegaDigestMatchKind.KeywordOverlap;
                    bestConfidence = keywordConf;
                    bestDetail = $"Keyword overlap: {overlap} token(s)";
                }
            }

            var union = msgTokens.Union(titleTokens).Count();
            if (union == 0)
                continue;

            var jaccard = (double)msgTokens.Intersect(titleTokens).Count() / union;
            if (jaccard >= 0.28 && jaccard > bestConfidence * 0.85)
            {
                var semConf = Math.Min(0.82, jaccard + 0.15);
                if (semConf > bestConfidence)
                {
                    best = entry;
                    bestKind = MegaDigestMatchKind.SemanticSimilarity;
                    bestConfidence = semConf;
                    bestDetail = $"Semantic similarity (Jaccard {jaccard:F2})";
                }
            }
        }

        return new MegaDigestMatchResult(best, bestKind, bestConfidence, bestDetail);
    }

    private static string NormalizePhrase(string text) =>
        string.Join(' ', text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '-', '(', ')', '"', '\r', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries));

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in text.ToLowerInvariant().Split(
                     new[] { ' ', ',', '.', '?', '!', ';', ':', '-', '(', ')', '"', '\'', '\r', '\n', '\t' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length < 3 || StopWords.Contains(part))
                continue;
            tokens.Add(part);
        }

        return tokens;
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "are", "was", "were", "has", "have", "had", "not", "but", "with", "from", "this", "that",
        "which", "what", "when", "where", "who", "how", "many", "much", "any", "all", "can", "you", "our", "your",
        "show", "list", "give", "get", "tell", "find", "see", "use", "using", "into", "over", "under", "above",
        "below", "than", "then", "also", "only", "just", "about", "been", "being", "does", "did", "will", "would",
        "could", "should", "may", "might", "must", "per", "via", "out", "off", "top"
    };

    private static MegaDigestCatalog Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "WizAccountant.Api.Insight.Data.mega-digest-catalog.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var source = root.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";

        var list = new List<MegaDigestEntry>();
        if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in entries.EnumerateArray())
            {
                var id = e.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                var domain = e.TryGetProperty("domain", out var d) ? d.GetString() ?? "" : "";
                var title = e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                if (id > 0 && !string.IsNullOrWhiteSpace(title))
                    list.Add(new MegaDigestEntry(id, domain, title));
            }
        }

        return new MegaDigestCatalog(list, source);
    }
}

internal sealed record MegaDigestEntry(int Id, string Domain, string Title);
