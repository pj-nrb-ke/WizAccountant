using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Api;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class InsightFeedbackServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly InsightQueryLogService _service;

    public InsightFeedbackServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _service = new InsightQueryLogService(_db);
    }

    [Fact]
    public async Task SetFeedback_saves_rating_and_note()
    {
        var logId = await SeedLogAsync();
        var (found, duplicate) = await _service.SetFeedbackAsync(
            logId, "wrong", "wrong_route", "wanted monthly product analysis", CancellationToken.None);

        Assert.True(found);
        Assert.False(duplicate);
        var row = await _db.InsightQueryLogs.FindAsync(logId);
        Assert.Equal("wrong", row!.FeedbackRating);
        Assert.Contains("wrong_route", row.FeedbackNote!);
        Assert.Contains("monthly product", row.FeedbackNote!);
    }

    [Fact]
    public async Task SetFeedback_blocks_duplicate_submission()
    {
        var logId = await SeedLogAsync();
        await _service.SetFeedbackAsync(logId, "helpful", null, null, CancellationToken.None);
        var (found, duplicate) = await _service.SetFeedbackAsync(
            logId, "wrong", null, "second try", CancellationToken.None);

        Assert.True(found);
        Assert.True(duplicate);
        var row = await _db.InsightQueryLogs.FindAsync(logId);
        Assert.Equal("helpful", row!.FeedbackRating);
    }

    [Fact]
    public async Task SetFeedback_returns_not_found_for_unknown_id()
    {
        var (found, duplicate) = await _service.SetFeedbackAsync(
            Guid.NewGuid(), "helpful", null, null, CancellationToken.None);
        Assert.False(found);
        Assert.False(duplicate);
    }

    [Fact]
    public void Insight_index_includes_feedback_buttons()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "WizAccountant.Api", "wwwroot", "insight", "index.html"));
        Assert.True(File.Exists(path), path);
        var html = File.ReadAllText(path);
        Assert.Contains("id=\"chat-feedback\"", html);
        Assert.Contains("data-rating=\"helpful\"", html);
        Assert.Contains("data-rating=\"wrong\"", html);
        Assert.Contains("data-rating=\"needs_improvement\"", html);
        Assert.Contains("chat-feedback-reason", html);
    }

    private async Task<Guid> SeedLogAsync()
    {
        return await _service.LogAsync(new InsightQueryLogEntry
        {
            TenantId = "t1",
            SiteId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            UserQuery = "test",
            Operation = ProductOrderAnalysisChatMatcher.Operation,
            RouteStatus = "ok",
            InsightChatVersion = InsightChatInfo.Version,
        }, CancellationToken.None);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
