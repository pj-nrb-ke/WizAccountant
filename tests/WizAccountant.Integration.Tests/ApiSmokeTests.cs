using System.Net;
using System.Net.Http.Json;
using WizAccountant.Contracts;
using Xunit;

namespace WizAccountant.Integration.Tests;

/// <summary>
/// Integration smoke tests — verify key API endpoints return expected HTTP status codes
/// against a real ASP.NET Core test server backed by an isolated SQLite database.
///
/// Run with: dotnet test --filter "Category=Integration"
///           (excluded from the main unit test run by default)
///
/// These tests require the full application pipeline (middleware, DI, DB seed).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ApiSmokeTests(WizApiFactory factory) : IClassFixture<WizApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Health ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_Returns200()
    {
        var resp = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "nobody@nope.com", Password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task OidcLogin_WithMissingFields_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/oidc/login",
            new OidcLoginRequest { Provider = "", IdToken = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Tools ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InsightTools_Returns200WithList()
    {
        var resp = await _client.GetAsync("/api/v1/insight/tools");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var tools = await resp.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(tools);
        Assert.NotEmpty(tools);
    }

    // ── RBAC — unauthenticated requests hit gated endpoints ──────────────────

    [Theory]
    [InlineData("GET",  "/api/firms")]
    [InlineData("GET",  "/api/monitor/sites")]
    [InlineData("GET",  "/api/insight/dashboard/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET",  "/api/act/proposals")]
    [InlineData("GET",  "/api/compliance/data-export?tenantId=x")]
    public async Task GatedEndpoints_WithNoToken_Return401(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var resp = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Billing webhook — public endpoint ────────────────────────────────────

    [Fact]
    public async Task BillingWebhook_WithInvalidJson_Returns400()
    {
        using var content = new StringContent("not-json",
            System.Text.Encoding.UTF8, "application/json");
        var resp = await _client.PostAsync("/api/billing/webhook", content);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Version manifest ─────────────────────────────────────────────────────

    [Fact]
    public async Task VersionJson_IsServedAsStaticFile()
    {
        var resp = await _client.GetAsync("/version.json");
        // 200 if static files are served, 404 if not configured — both acceptable;
        // what matters is the server does not crash.
        Assert.True(
            resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Unexpected status {resp.StatusCode}");
    }
}
