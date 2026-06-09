using System.Data;
using Microsoft.EntityFrameworkCore;
using BC = BCrypt.Net.BCrypt;

namespace WizAccountant.Api;

public static class DbSeed
{
    public static async Task EnsureUsersRoleColumnAsync(AppDbContext db)
    {
        if (!await SqliteTableExistsAsync(db, "Users"))
            return;

        if (await SqliteColumnExistsAsync(db, "Users", "Role"))
            return;

        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'Preparer';");
    }

    private static async Task<bool> SqliteTableExistsAsync(AppDbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var param = command.CreateParameter();
        param.ParameterName = "$name";
        param.Value = tableName;
        command.Parameters.Add(param);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    private static async Task<bool> SqliteColumnExistsAsync(AppDbContext db, string tableName, string columnName)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name = $name";
        var param = command.CreateParameter();
        param.ParameterName = "$name";
        param.Value = columnName;
        command.Parameters.Add(param);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public static async Task EnsureJobAuditsTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS JobAudits (
                AuditId TEXT NOT NULL PRIMARY KEY,
                JobId TEXT NOT NULL,
                SiteId TEXT NOT NULL,
                Operation TEXT NOT NULL,
                EventType TEXT NOT NULL,
                RequestedBy TEXT NULL,
                Success INTEGER NULL,
                Detail TEXT NULL,
                TimestampUtc TEXT NOT NULL
            );
            """);
    }

    /// <summary>
    /// Hashes any remaining plain-text passwords (passwords not starting with $2).
    /// Runs at startup to handle databases created before the BCrypt upgrade.
    /// </summary>
    public static async Task MigratePasswordHashesAsync(AppDbContext db)
    {
        var plainTextUsers = await db.Users
            .Where(u => !u.Password.StartsWith("$2"))
            .ToListAsync();

        foreach (var user in plainTextUsers)
            user.Password = BC.HashPassword(user.Password, workFactor: 12);

        if (plainTextUsers.Count > 0)
            await db.SaveChangesAsync();
    }

    public static async Task EnsurePhase1SeedAsync(AppDbContext db)
    {
        await EnsureJobAuditsTableAsync(db);
        await EnsureInsightQueryLogTableAsync(db);
        await EnsureInsightSavedSqlQueriesTableAsync(db);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS Tenants (
                TenantId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Users (
                UserId TEXT NOT NULL PRIMARY KEY,
                TenantId TEXT NOT NULL,
                Email TEXT NOT NULL,
                Password TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'Preparer'
            );
            """);

        // Legacy DBs: Users table without Role (new CREATE above already includes Role).
        await EnsureUsersRoleColumnAsync(db);

        if (!await db.Tenants.AnyAsync())
        {
            db.Tenants.Add(new TenantRecord { TenantId = "pilot-tenant", Name = "Pilot Tenant" });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new UserRecord
            {
                UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                TenantId = "pilot-tenant",
                Email = "admin@pilot.local",
                Password = BC.HashPassword("pilot", workFactor: 12),
                DisplayName = "Pilot Admin",
                Role = "Admin"
            });
            await db.SaveChangesAsync();
        }

        await EnsurePhase2TablesAsync(db);
        await EnsurePhase3TablesAsync(db);
        await EnsurePhase3UsersAsync(db);
    }

    public static async Task EnsureInsightQueryLogTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS InsightQueryLogs (
                LogId TEXT NOT NULL PRIMARY KEY,
                TenantId TEXT NOT NULL,
                SiteId TEXT NOT NULL,
                ConversationId TEXT NOT NULL,
                UserQuery TEXT NOT NULL,
                Operation TEXT NULL,
                RouteStatus TEXT NOT NULL,
                BusinessProcess TEXT NULL,
                ContractJson TEXT NULL,
                ToolsUsedJson TEXT NULL,
                JobStatus TEXT NULL,
                ErrorSummary TEXT NULL,
                InsightChatVersion TEXT NOT NULL,
                CompatibilityBlocked INTEGER NOT NULL DEFAULT 0,
                CompatibilityReason TEXT NULL,
                FeedbackRating TEXT NULL,
                FeedbackNote TEXT NULL,
                FeedbackAtUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            """);
    }

    public static async Task EnsureInsightSavedSqlQueriesTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS InsightSavedSqlQueries (
                QueryId TEXT NOT NULL PRIMARY KEY,
                TenantId TEXT NOT NULL,
                SiteId TEXT NOT NULL,
                Title TEXT NOT NULL,
                AiPrompt TEXT NULL,
                Sql TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_InsightSavedSqlQueries_TenantSite
                ON InsightSavedSqlQueries (TenantId, SiteId);
            """);
    }

    public static async Task EnsurePhase2TablesAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS ChatConversations (
                ConversationId TEXT NOT NULL PRIMARY KEY,
                TenantId TEXT NOT NULL,
                SiteId TEXT NOT NULL,
                Title TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS ChatMessages (
                MessageId TEXT NOT NULL PRIMARY KEY,
                ConversationId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                ToolsUsedJson TEXT NULL,
                TimestampUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS NotificationLogs (
                NotificationId TEXT NOT NULL PRIMARY KEY,
                SiteId TEXT NOT NULL,
                EventType TEXT NOT NULL,
                Email TEXT NOT NULL,
                Status TEXT NOT NULL,
                TimestampUtc TEXT NOT NULL
            );
            """);
    }

    public static async Task EnsurePhase3UsersAsync(AppDbContext db)
    {
        var admin = await db.Users.FindAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        if (admin is not null)
        {
            admin.Role = "Admin";
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Email == "approver@pilot.local"))
        {
            db.Users.Add(new UserRecord
            {
                UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                TenantId = "pilot-tenant",
                Email = "approver@pilot.local",
                Password = BC.HashPassword("pilot", workFactor: 12),
                DisplayName = "Pilot Approver",
                Role = "Approver"
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Email == "preparer@pilot.local"))
        {
            db.Users.Add(new UserRecord
            {
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                TenantId = "pilot-tenant",
                Email = "preparer@pilot.local",
                Password = BC.HashPassword("pilot", workFactor: 12),
                DisplayName = "Pilot Preparer",
                Role = "Preparer"
            });
            await db.SaveChangesAsync();
        }
    }

    public static async Task EnsurePhase3TablesAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS ApprovalProposals (
                ProposalId TEXT NOT NULL PRIMARY KEY,
                SiteId TEXT NOT NULL,
                TenantId TEXT NOT NULL,
                ProposalType TEXT NOT NULL,
                Title TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                Status INTEGER NOT NULL,
                PreparedByUserId TEXT NOT NULL,
                ApprovedByUserId TEXT NULL,
                IdempotencyKey TEXT NULL,
                JobId TEXT NULL,
                Comment TEXT NULL,
                RejectReason TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                ResolvedAtUtc TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS WriteAudits (
                WriteAuditId TEXT NOT NULL PRIMARY KEY,
                ProposalId TEXT NOT NULL,
                SiteId TEXT NOT NULL,
                Operation TEXT NOT NULL,
                BeforeJson TEXT NULL,
                AfterJson TEXT NULL,
                PreparerUserId TEXT NULL,
                ApproverUserId TEXT NULL,
                EvolutionRef TEXT NULL,
                Success INTEGER NOT NULL,
                TimestampUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS SiteConfigs (
                SiteId TEXT NOT NULL PRIMARY KEY,
                ConfigJson TEXT NOT NULL,
                SyncedAtUtc TEXT NOT NULL
            );
            """);
    }
}
