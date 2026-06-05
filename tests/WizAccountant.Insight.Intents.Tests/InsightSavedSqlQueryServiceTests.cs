using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Api;
using WizAccountant.Api.Insight;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class InsightSavedSqlQueryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly InsightSavedSqlQueryService _service;
    private readonly Guid _siteId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public InsightSavedSqlQueryServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _service = new InsightSavedSqlQueryService(_db);
        _db.Sites.Add(new SiteRecord
        {
            SiteId = _siteId,
            TenantId = "pilot-tenant",
            SiteName = "Test",
            DeviceId = "dev",
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Upsert_and_list_roundtrip()
    {
        var created = await _service.UpsertAsync("pilot-tenant", new UpsertInsightSavedSqlQueryRequest
        {
            SiteId = _siteId,
            Title = "Product monthly",
            AiPrompt = "which product ordered most per month",
            Sql = "SELECT 1"
        }, CancellationToken.None);

        var list = await _service.ListAsync("pilot-tenant", _siteId, CancellationToken.None);
        Assert.Single(list);
        Assert.Equal(created.QueryId, list[0].QueryId);
        Assert.Equal("Product monthly", list[0].Title);
    }

    [Fact]
    public async Task Upsert_updates_existing_row()
    {
        var created = await _service.UpsertAsync("pilot-tenant", new UpsertInsightSavedSqlQueryRequest
        {
            SiteId = _siteId,
            Title = "V1",
            Sql = "SELECT 1"
        }, CancellationToken.None);

        var updated = await _service.UpsertAsync("pilot-tenant", new UpsertInsightSavedSqlQueryRequest
        {
            QueryId = created.QueryId,
            SiteId = _siteId,
            Title = "V2",
            Sql = "SELECT 2"
        }, CancellationToken.None);

        Assert.Equal(created.QueryId, updated.QueryId);
        Assert.Equal("V2", updated.Title);
        Assert.Equal("SELECT 2", updated.Sql);
    }

    [Fact]
    public async Task Delete_removes_row()
    {
        var created = await _service.UpsertAsync("pilot-tenant", new UpsertInsightSavedSqlQueryRequest
        {
            SiteId = _siteId,
            Title = "Delete me",
            Sql = "SELECT 1"
        }, CancellationToken.None);

        Assert.True(await _service.DeleteAsync("pilot-tenant", _siteId, created.QueryId, CancellationToken.None));
        Assert.Empty(await _service.ListAsync("pilot-tenant", _siteId, CancellationToken.None));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
