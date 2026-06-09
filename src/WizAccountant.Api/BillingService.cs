using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace WizAccountant.Api;

/// <summary>
/// Phase 4 Block 4 (Task #19) — tenant billing and subscription management.
///
/// Handles incoming webhooks from Stripe/Paddle and enforces plan-based feature gating.
/// Webhook signature verification is provider-specific and marked TODO for production key config.
///
/// Plans:
///   free       — default, no billing required, limited features
///   pro        — paid, single-tenant, full Insight + Act + monitoring
///   enterprise — paid, multi-tenant firm, all features including SSO + multi-site
/// </summary>
public sealed class BillingService(AppDbContext db, ILogger<BillingService> logger)
{
    // ── Feature gate constants ───────────────────────────────────────────────

    public static readonly HashSet<string> ProFeatures =
        ["insight.chat", "act.proposals", "monitoring", "export"];

    public static readonly HashSet<string> EnterpriseFeatures =
        ["sso", "multisite", "firm.management", "mobile.advanced"];

    // ── Subscription queries ─────────────────────────────────────────────────

    public async Task<SubscriptionDto> GetSubscriptionAsync(string tenantId, CancellationToken ct)
    {
        var sub = await db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (sub is null)
            return new SubscriptionDto(tenantId, "free", "active", null, null);

        return new SubscriptionDto(
            TenantId: sub.TenantId,
            Plan: sub.Plan,
            Status: sub.Status,
            BillingRef: sub.BillingRef,
            CurrentPeriodEnd: sub.CurrentPeriodEnd);
    }

    public async Task<bool> IsFeatureEnabledAsync(string tenantId, string feature, CancellationToken ct)
    {
        var sub = await GetSubscriptionAsync(tenantId, ct);
        if (sub.Status is not ("active" or "trialing")) return false;

        return sub.Plan switch
        {
            "enterprise" => true, // all features
            "pro" => ProFeatures.Contains(feature),
            _ => false, // free plan — no paid features
        };
    }

    // ── Webhook processing ───────────────────────────────────────────────────

    /// <summary>
    /// Process a billing webhook event (Stripe/Paddle).
    /// eventType: e.g. "customer.subscription.created", "customer.subscription.deleted",
    ///            "invoice.payment_failed", "customer.subscription.updated"
    /// payload: raw JSON event body from provider.
    /// </summary>
    public async Task HandleWebhookAsync(string eventType, JsonDocument payload, CancellationToken ct)
    {
        // TODO: verify webhook signature (Stripe-Signature header / Paddle hmac) before calling this

        logger.LogInformation("Billing webhook received: {EventType}", eventType);

        var root = payload.RootElement;
        var tenantId = ExtractTenantId(root);
        if (tenantId is null)
        {
            logger.LogWarning("Billing webhook {EventType} has no resolvable tenantId — skipped.", eventType);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // Upsert subscription record
        var sub = await db.Subscriptions.FindAsync([tenantId], ct);
        if (sub is null)
        {
            sub = new SubscriptionRecord { TenantId = tenantId };
            db.Subscriptions.Add(sub);
        }

        sub.BillingRef = TryGet(root, "id") ?? TryGet(root, "subscription_id") ?? sub.BillingRef;
        sub.UpdatedAtUtc = now;

        switch (eventType)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
                sub.Plan = ResolvePlan(root);
                sub.Status = TryGet(root, "status") ?? "active";
                sub.CurrentPeriodEnd = TryGetDate(root, "current_period_end");
                break;

            case "customer.subscription.deleted":
                sub.Status = "cancelled";
                sub.CurrentPeriodEnd = null;
                break;

            case "invoice.payment_failed":
                sub.Status = "past_due";
                break;

            case "invoice.payment_succeeded":
                if (sub.Status == "past_due") sub.Status = "active";
                break;

            default:
                logger.LogDebug("Billing webhook {EventType} — no action taken.", eventType);
                break;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Subscription {TenantId} updated: plan={Plan} status={Status}",
            tenantId, sub.Plan, sub.Status);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string? ExtractTenantId(JsonElement root)
    {
        // WizAccountant stores tenantId in Stripe metadata.wizTenantId
        if (root.TryGetProperty("metadata", out var meta) &&
            meta.TryGetProperty("wizTenantId", out var tid))
            return tid.GetString();

        // Paddle: passthrough field
        if (root.TryGetProperty("passthrough", out var pt))
        {
            try
            {
                var inner = JsonDocument.Parse(pt.GetString() ?? "{}");
                if (inner.RootElement.TryGetProperty("wizTenantId", out var t))
                    return t.GetString();
            }
            catch { /* ignore */ }
        }

        return null;
    }

    private static string ResolvePlan(JsonElement root)
    {
        // Stripe: items.data[0].price.lookup_key or nickname
        if (root.TryGetProperty("items", out var items) &&
            items.TryGetProperty("data", out var data) &&
            data.GetArrayLength() > 0)
        {
            var first = data[0];
            if (first.TryGetProperty("price", out var price))
            {
                var key = TryGet(price, "lookup_key") ?? TryGet(price, "nickname") ?? "";
                if (key.Contains("enterprise", StringComparison.OrdinalIgnoreCase)) return "enterprise";
                if (key.Contains("pro", StringComparison.OrdinalIgnoreCase)) return "pro";
            }
        }

        // Paddle: plan_id fallback
        if (root.TryGetProperty("plan_id", out var planId))
        {
            var pid = planId.GetString() ?? "";
            if (pid.Contains("enterprise")) return "enterprise";
            if (pid.Contains("pro")) return "pro";
        }

        return "pro"; // default for paid subscription events
    }

    private static string? TryGet(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) ? v.GetString() : null;

    private static DateTimeOffset? TryGetDate(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number)
            return DateTimeOffset.FromUnixTimeSeconds(v.GetInt64());
        return null;
    }
}

public sealed record SubscriptionDto(
    string TenantId,
    string Plan,
    string Status,
    string? BillingRef,
    DateTimeOffset? CurrentPeriodEnd
);
