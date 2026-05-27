using System.Collections.Concurrent;

namespace WizAccountant.Api.Middleware;

/// <summary>Phase 2: simple per-site rate limit for job endpoints.</summary>
public sealed class SiteRateLimitMiddleware(RequestDelegate next, ILogger<SiteRateLimitMiddleware> logger)
{
    private static readonly ConcurrentDictionary<Guid, Queue<DateTimeOffset>> Buckets = new();
    private const int MaxPerMinute = 120;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/jobs") &&
            !context.Request.Path.StartsWithSegments("/api/insight"))
        {
            await next(context);
            return;
        }

        if (!TryGetSiteId(context, out var siteId))
        {
            await next(context);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var q = Buckets.GetOrAdd(siteId, _ => new Queue<DateTimeOffset>());
        lock (q)
        {
            while (q.Count > 0 && now - q.Peek() > TimeSpan.FromMinutes(1))
                q.Dequeue();

            if (q.Count >= MaxPerMinute)
            {
                logger.LogWarning("Rate limit exceeded for site {SiteId}", siteId);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            q.Enqueue(now);
        }

        await next(context);
    }

    private static bool TryGetSiteId(HttpContext context, out Guid siteId)
    {
        siteId = Guid.Empty;
        if (context.Request.Query.TryGetValue("siteId", out var q) && Guid.TryParse(q, out siteId))
            return true;

        if (context.Request.RouteValues.TryGetValue("siteId", out var rv) && rv is string s && Guid.TryParse(s, out siteId))
            return true;

        return false;
    }
}
