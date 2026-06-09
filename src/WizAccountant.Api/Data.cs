using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SiteRecord> Sites => Set<SiteRecord>();
    public DbSet<PairingCodeRecord> PairingCodes => Set<PairingCodeRecord>();
    public DbSet<JobRecord> Jobs => Set<JobRecord>();
    public DbSet<JobAuditRecord> JobAudits => Set<JobAuditRecord>();
    public DbSet<TenantRecord> Tenants => Set<TenantRecord>();
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<ChatConversationRecord> ChatConversations => Set<ChatConversationRecord>();
    public DbSet<ChatMessageRecord> ChatMessages => Set<ChatMessageRecord>();
    public DbSet<InsightQueryLogRecord> InsightQueryLogs => Set<InsightQueryLogRecord>();
    public DbSet<NotificationLogRecord> NotificationLogs => Set<NotificationLogRecord>();
    public DbSet<ApprovalProposalRecord> ApprovalProposals => Set<ApprovalProposalRecord>();
    public DbSet<WriteAuditRecord> WriteAudits => Set<WriteAuditRecord>();
    public DbSet<SiteConfigRecord> SiteConfigs => Set<SiteConfigRecord>();
    public DbSet<InsightSavedSqlQueryRecord> InsightSavedSqlQueries => Set<InsightSavedSqlQueryRecord>();
    public DbSet<FirmRecord> Firms => Set<FirmRecord>();
    public DbSet<ExternalIdentityRecord> ExternalIdentities => Set<ExternalIdentityRecord>();
    public DbSet<SubscriptionRecord> Subscriptions => Set<SubscriptionRecord>();
    public DbSet<PushTokenRecord> PushTokens => Set<PushTokenRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SiteRecord>().HasKey(x => x.SiteId);
        modelBuilder.Entity<PairingCodeRecord>().HasKey(x => x.PairingCodeId);
        modelBuilder.Entity<JobRecord>().HasKey(x => x.JobId);
        modelBuilder.Entity<JobAuditRecord>().HasKey(x => x.AuditId);
        modelBuilder.Entity<TenantRecord>().HasKey(x => x.TenantId);
        modelBuilder.Entity<UserRecord>().HasKey(x => x.UserId);
        modelBuilder.Entity<ChatConversationRecord>().HasKey(x => x.ConversationId);
        modelBuilder.Entity<ChatMessageRecord>().HasKey(x => x.MessageId);
        modelBuilder.Entity<InsightQueryLogRecord>().HasKey(x => x.LogId);
        modelBuilder.Entity<NotificationLogRecord>().HasKey(x => x.NotificationId);
        modelBuilder.Entity<ApprovalProposalRecord>().HasKey(x => x.ProposalId);
        modelBuilder.Entity<WriteAuditRecord>().HasKey(x => x.WriteAuditId);
        modelBuilder.Entity<SiteConfigRecord>().HasKey(x => x.SiteId);
        modelBuilder.Entity<InsightSavedSqlQueryRecord>().HasKey(x => x.QueryId);
        modelBuilder.Entity<FirmRecord>().HasKey(x => x.FirmId);
        modelBuilder.Entity<ExternalIdentityRecord>().HasKey(x => x.ExternalIdentityId);
        modelBuilder.Entity<SubscriptionRecord>().HasKey(x => x.TenantId);
        modelBuilder.Entity<PushTokenRecord>().HasKey(x => x.PushTokenId);
    }
}

public sealed class SiteRecord
{
    public Guid SiteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
}

public sealed class PairingCodeRecord
{
    public Guid PairingCodeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsUsed { get; set; }
}

public sealed class JobRecord
{
    public Guid JobId { get; set; }
    public Guid SiteId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? RequestedBy { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class JobAuditRecord
{
    public Guid AuditId { get; set; }
    public Guid JobId { get; set; }
    public Guid SiteId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public bool? Success { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
}

public sealed class TenantRecord
{
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Firm this tenant belongs to, or null for standalone tenants.</summary>
    public string? FirmId { get; set; }
}

/// <summary>
/// An accounting firm that may own multiple tenants/sites.
/// When IsPracticeMode = true, write operations are blocked across all firm tenants.
/// </summary>
public sealed class FirmRecord
{
    public string FirmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>When true, Act proposals are blocked — read-only demo/training mode.</summary>
    public bool IsPracticeMode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class UserRecord
{
    public Guid UserId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Preparer";
}

public sealed class ChatConversationRecord
{
    public Guid ConversationId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid SiteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ChatMessageRecord
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ToolsUsedJson { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
}

/// <summary>Insight ask audit trail for self-training triage (Layer 6).</summary>
public sealed class InsightQueryLogRecord
{
    public Guid LogId { get; set; }
    public string TenantId { get; set; } = "";
    public Guid SiteId { get; set; }
    public Guid ConversationId { get; set; }
    public string UserQuery { get; set; } = "";
    public string? Operation { get; set; }
    public string RouteStatus { get; set; } = "";
    public string? BusinessProcess { get; set; }
    public string? ContractJson { get; set; }
    public string? ToolsUsedJson { get; set; }
    public string? JobStatus { get; set; }
    public string? ErrorSummary { get; set; }
    public string InsightChatVersion { get; set; } = "";
    public bool CompatibilityBlocked { get; set; }
    public string? CompatibilityReason { get; set; }
    public string? FeedbackRating { get; set; }
    public string? FeedbackNote { get; set; }
    public string? FeedbackAtUtc { get; set; }
    /// <summary>ISO-8601 UTC string for SQLite-safe ordering/filtering.</summary>
    public string CreatedAtUtc { get; set; } = "";
}

public sealed class NotificationLogRecord
{
    public Guid NotificationId { get; set; }
    public Guid SiteId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
}

public sealed class ApprovalProposalRecord
{
    public Guid ProposalId { get; set; }
    public Guid SiteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ProposalType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public Guid PreparedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? JobId { get; set; }
    public string? Comment { get; set; }
    public string? RejectReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public sealed class WriteAuditRecord
{
    public Guid WriteAuditId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid SiteId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public Guid? PreparerUserId { get; set; }
    public Guid? ApproverUserId { get; set; }
    public string? EvolutionRef { get; set; }
    public bool Success { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
}

public sealed class SiteConfigRecord
{
    public Guid SiteId { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public DateTimeOffset SyncedAtUtc { get; set; }
}

/// <summary>Insight SQL tab — saved queries linked to AI Assistant prompts (tenant + site scoped).</summary>
public sealed class InsightSavedSqlQueryRecord
{
    public Guid QueryId { get; set; }
    public string TenantId { get; set; } = "";
    public Guid SiteId { get; set; }
    public string Title { get; set; } = "";
    public string? AiPrompt { get; set; }
    public string Sql { get; set; } = "";
    public string CreatedAtUtc { get; set; } = "";
    public string UpdatedAtUtc { get; set; } = "";
}

/// <summary>
/// Phase 4 Block 4 (Task #18) — maps an external OIDC identity (Provider + Subject)
/// to a WizAccountant user, enabling SSO login without storing the provider's credentials.
/// </summary>
public sealed class ExternalIdentityRecord
{
    public Guid ExternalIdentityId { get; set; }
    /// <summary>OIDC provider name: "AzureAD" or "Google".</summary>
    public string Provider { get; set; } = string.Empty;
    /// <summary>Stable external user identifier (JWT "sub" claim).</summary>
    public string Subject { get; set; } = string.Empty;
    /// <summary>The WizAccountant user this external identity maps to.</summary>
    public Guid UserId { get; set; }
    /// <summary>Email as returned by the provider at link time.</summary>
    public string ProviderEmail { get; set; } = string.Empty;
    public DateTimeOffset LinkedAtUtc { get; set; }
    public DateTimeOffset LastLoginAtUtc { get; set; }
}

/// <summary>
/// Phase 4 Block 4 (Task #19) — tenant subscription state.
/// Updated by billing webhooks (Stripe/Paddle). Gating enforced in BillingService.
/// </summary>
public sealed class SubscriptionRecord
{
    /// <summary>Primary key — one subscription per tenant.</summary>
    public string TenantId { get; set; } = string.Empty;
    /// <summary>Plan name: "free" | "pro" | "enterprise".</summary>
    public string Plan { get; set; } = "free";
    /// <summary>Status: "active" | "cancelled" | "past_due" | "trialing".</summary>
    public string Status { get; set; } = "active";
    /// <summary>Billing provider reference (Stripe subscription ID etc.).</summary>
    public string? BillingRef { get; set; }
    /// <summary>When the current billing period ends (null for free/lifetime plans).</summary>
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>M4 — Expo push token registered by mobile app clients.</summary>
public sealed class PushTokenRecord
{
    public Guid PushTokenId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Expo push token string: ExponentPushToken[xxx] or ea:xxx</summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>"ios" | "android"</summary>
    public string Platform { get; set; } = string.Empty;
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
}
