using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

public sealed class ReadOnlyChatService(AppDbContext db, JobService jobs, ILogger<ReadOnlyChatService> logger)
{
    private const string Guardrail =
        "I only read live Sage data. I cannot post journals or payments. Ask an approver in Phase 3 for writes.";

    public async Task<ChatMessageResponse> AskAsync(ChatMessageRequest request, string? tenantId, CancellationToken ct)
    {
        var site = await db.Sites.FindAsync([request.SiteId], ct);
        if (site is null) throw new InvalidOperationException("Site not found.");

        var conversation = request.ConversationId is { } cid
            ? await db.ChatConversations.FindAsync([cid], ct)
            : null;

        if (conversation is null)
        {
            conversation = new ChatConversationRecord
            {
                ConversationId = Guid.NewGuid(),
                TenantId = tenantId ?? site.TenantId,
                SiteId = request.SiteId,
                Title = Truncate(request.Message, 60),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            db.ChatConversations.Add(conversation);
        }

        db.ChatMessages.Add(new ChatMessageRecord
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversation.ConversationId,
            Role = "user",
            Content = request.Message,
            TimestampUtc = DateTimeOffset.UtcNow
        });

        var (operation, parameters, toolsUsed) = PlanToolCall(request.Message);
        string reply;
        var citations = new List<string>();
        var dataAsOf = DateTimeOffset.UtcNow;

        if (operation is null)
        {
            reply = $"I can help with customers, suppliers, open items, and dashboard KPIs on {site.SiteName}. " +
                    "Try: \"show dashboard\", \"list customers\", \"open items for CASH\", or \"search ACME\". " +
                    Guardrail;
        }
        else if (!InsightReadOnlyTools.IsAllowed(operation))
        {
            reply = "That action is not in the read-only allowlist. " + Guardrail;
        }
        else
        {
            try
            {
                var job = await jobs.RunAndWaitAsync(new CreateJobRequest
                {
                    SiteId = request.SiteId,
                    Operation = operation,
                    Parameters = parameters,
                    RequestedBy = "insight-chat"
                }, 90, ct);

                dataAsOf = job.UpdatedAtUtc ?? job.CreatedAtUtc;
                if (job.Status == JobStatus.Failed)
                {
                    reply = $"Sage read failed: {job.Error}. " + Guardrail;
                }
                else
                {
                    (reply, citations) = FormatJobResult(operation, job.ResultJson);
                    reply += $"\n\n(Data as of {dataAsOf:u})";
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Chat tool {Operation} failed", operation);
                reply = $"Could not complete read ({ex.Message}). Is the connector online? " + Guardrail;
            }
        }

        db.ChatMessages.Add(new ChatMessageRecord
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversation.ConversationId,
            Role = "assistant",
            Content = reply,
            ToolsUsedJson = JsonSerializer.Serialize(toolsUsed),
            TimestampUtc = DateTimeOffset.UtcNow
        });
        conversation.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return new ChatMessageResponse
        {
            ConversationId = conversation.ConversationId,
            Reply = reply,
            ToolsUsed = toolsUsed,
            Citations = citations,
            DataAsOfUtc = dataAsOf,
            GuardrailNotice = Guardrail
        };
    }

    public async Task<List<ConversationDto>> ListConversationsAsync(string tenantId, Guid siteId, CancellationToken ct) =>
        await db.ChatConversations.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.SiteId == siteId)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Take(50)
            .Select(c => new ConversationDto
            {
                ConversationId = c.ConversationId,
                Title = c.Title,
                UpdatedAtUtc = c.UpdatedAtUtc
            })
            .ToListAsync(ct);

    private static (string? operation, Dictionary<string, string> parameters, List<string> tools) PlanToolCall(string message)
    {
        var m = message.ToLowerInvariant();
        var tools = new List<string>();
        var parameters = new Dictionary<string, string>();

        if (m.Contains("dashboard") || m.Contains("kpi") || m.Contains("summary"))
        {
            tools.Add("dashboard.summary");
            return ("dashboard.summary", parameters, tools);
        }

        if (m.StartsWith("search ") || m.Contains("find "))
        {
            var query = Regex.Replace(message, "(?i)^(search|find)\\s+", "").Trim();
            parameters["query"] = query;
            tools.Add("search.global");
            return ("search.global", parameters, tools);
        }

        if (m.Contains("open item") && m.Contains("supplier"))
        {
            ExtractAccount(m, parameters);
            tools.Add("supplier.openitems");
            return ("supplier.openitems", parameters, tools);
        }

        if (m.Contains("open item") || m.Contains("outstanding"))
        {
            ExtractAccount(m, parameters);
            tools.Add("customer.openitems");
            return ("customer.openitems", parameters, tools);
        }

        if (m.Contains("supplier") && m.Contains("list"))
        {
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        if (m.Contains("customer") && m.Contains("list"))
        {
            tools.Add("customer.list");
            return ("customer.list", parameters, tools);
        }

        if (m.Contains("customer"))
        {
            tools.Add("customer.list");
            return ("customer.list", parameters, tools);
        }

        if (m.Contains("supplier"))
        {
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        return (null, parameters, tools);
    }

    private static void ExtractAccount(string message, Dictionary<string, string> parameters)
    {
        var match = Regex.Match(message, @"\b([A-Z0-9]{2,12})\b");
        if (match.Success)
            parameters["account"] = match.Groups[1].Value;
    }

    private static (string reply, List<string> citations) FormatJobResult(string operation, string? resultJson)
    {
        var citations = new List<string>();
        if (string.IsNullOrWhiteSpace(resultJson))
            return ("No data returned.", citations);

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                var count = items.GetArrayLength();
                citations.Add($"{operation}: {count} row(s)");
                var preview = items.EnumerateArray().Take(5)
                    .Select(e => e.TryGetProperty("code", out var c) ? c.GetString()
                        : e.TryGetProperty("account", out var a) ? a.GetString() : "?")
                    .Where(x => x is not null);
                return ($"Found {count} record(s). Sample: {string.Join(", ", preview)}.", citations);
            }

            if (root.TryGetProperty("hits", out var hits) && hits.ValueKind == JsonValueKind.Array)
            {
                var count = hits.GetArrayLength();
                citations.Add($"search: {count} hit(s)");
                return ($"Search returned {count} match(es).", citations);
            }

            if (root.TryGetProperty("kpis", out _))
            {
                citations.Add("dashboard.summary");
                return (resultJson, citations);
            }

            return (Truncate(resultJson, 800), citations);
        }
        catch
        {
            return (Truncate(resultJson, 800), citations);
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
