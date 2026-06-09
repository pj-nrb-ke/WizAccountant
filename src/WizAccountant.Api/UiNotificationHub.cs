using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace WizAccountant.Api;

// ── Hub ──────────────────────────────────────────────────────────────────────

/// <summary>
/// SignalR hub for real-time push notifications to browser UI clients.
/// Clients join their tenantId group on connect.
/// Events pushed: job-completed, approval-required, alert-threshold-hit.
/// </summary>
[Authorize]
public sealed class UiNotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirstValue("tenantId");
        if (!string.IsNullOrWhiteSpace(tenantId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirstValue("tenantId");
        if (!string.IsNullOrWhiteSpace(tenantId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
        await base.OnDisconnectedAsync(exception);
    }
}

// ── Notification envelope ────────────────────────────────────────────────────

/// <summary>Payload sent to UI clients via SignalR.</summary>
public sealed record UiPushEvent(
    string Event,           // "job-completed" | "approval-required" | "alert-threshold-hit"
    string? SiteId,
    string? JobId,
    string? ProposalId,
    string? Message,
    string TimestampUtc);

// ── Service ──────────────────────────────────────────────────────────────────

/// <summary>
/// Pushes real-time events to browser UI clients grouped by tenantId.
/// Replaces the stub notification service for production use.
/// </summary>
public sealed class WizNotificationService(
    IHubContext<UiNotificationHub> hub,
    ILogger<WizNotificationService> logger)
{
    /// <summary>Push a job-completed or job-failed notification to the tenant's browser clients.</summary>
    public async Task PushJobCompletedAsync(
        string tenantId, Guid siteId, Guid jobId, bool success, CancellationToken ct = default)
    {
        var evt = new UiPushEvent(
            Event: success ? "job-completed" : "job-failed",
            SiteId: siteId.ToString(),
            JobId: jobId.ToString(),
            ProposalId: null,
            Message: success ? "Job completed successfully." : "Job failed — check audit log.",
            TimestampUtc: DateTimeOffset.UtcNow.ToString("o"));

        await hub.Clients.Group($"tenant:{tenantId}")
            .SendAsync("notification", evt, cancellationToken: ct);

        logger.LogInformation(
            "UI push [{Event}] → tenant:{TenantId} job:{JobId}", evt.Event, tenantId, jobId);
    }

    /// <summary>Push an approval-required notification to the tenant's approvers.</summary>
    public async Task PushApprovalRequiredAsync(
        string tenantId, Guid proposalId, string title, CancellationToken ct = default)
    {
        var evt = new UiPushEvent(
            Event: "approval-required",
            SiteId: null,
            JobId: null,
            ProposalId: proposalId.ToString(),
            Message: $"Approval required: {title}",
            TimestampUtc: DateTimeOffset.UtcNow.ToString("o"));

        await hub.Clients.Group($"tenant:{tenantId}")
            .SendAsync("notification", evt, cancellationToken: ct);

        logger.LogInformation(
            "UI push [approval-required] → tenant:{TenantId} proposal:{ProposalId}",
            tenantId, proposalId);
    }

    /// <summary>Push an alert-threshold-hit notification (Insight anomaly detection).</summary>
    public async Task PushAlertThresholdAsync(
        string tenantId, Guid siteId, string message, CancellationToken ct = default)
    {
        var evt = new UiPushEvent(
            Event: "alert-threshold-hit",
            SiteId: siteId.ToString(),
            JobId: null,
            ProposalId: null,
            Message: message,
            TimestampUtc: DateTimeOffset.UtcNow.ToString("o"));

        await hub.Clients.Group($"tenant:{tenantId}")
            .SendAsync("notification", evt, cancellationToken: ct);

        logger.LogInformation(
            "UI push [alert-threshold-hit] → tenant:{TenantId} site:{SiteId}",
            tenantId, siteId);
    }
}
