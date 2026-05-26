using Microsoft.AspNetCore.SignalR;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

public interface IConnectorRegistry
{
    Task RegisterAsync(Guid siteId, string connectionId);
    Task MarkHeartbeatAsync(Guid siteId);
    Task UnregisterConnectionAsync(string connectionId);
    bool TryGetConnectionId(Guid siteId, out string? connectionId);
}

public sealed class ConnectorRegistry : IConnectorRegistry
{
    private readonly Dictionary<Guid, string> _siteConnections = new();
    private readonly Dictionary<string, Guid> _connectionSites = new();
    private readonly object _lock = new();

    public Task RegisterAsync(Guid siteId, string connectionId)
    {
        lock (_lock)
        {
            _siteConnections[siteId] = connectionId;
            _connectionSites[connectionId] = siteId;
        }
        return Task.CompletedTask;
    }

    public Task MarkHeartbeatAsync(Guid siteId)
    {
        // Heartbeat tracking persists in DB in hub call.
        return Task.CompletedTask;
    }

    public Task UnregisterConnectionAsync(string connectionId)
    {
        lock (_lock)
        {
            if (_connectionSites.TryGetValue(connectionId, out var siteId))
            {
                _connectionSites.Remove(connectionId);
                _siteConnections.Remove(siteId);
            }
        }
        return Task.CompletedTask;
    }

    public bool TryGetConnectionId(Guid siteId, out string? connectionId)
    {
        lock (_lock)
        {
            return _siteConnections.TryGetValue(siteId, out connectionId);
        }
    }
}

public sealed class ConnectorHub(AppDbContext db, IConnectorRegistry registry) : Hub
{
    public async Task RegisterSite(Guid siteId)
    {
        await registry.RegisterAsync(siteId, Context.ConnectionId);
        var site = await db.Sites.FindAsync(siteId);
        if (site is not null)
        {
            site.IsOnline = true;
            site.LastSeenUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task Heartbeat(ConnectorHeartbeat heartbeat)
    {
        await registry.MarkHeartbeatAsync(heartbeat.SiteId);
        var site = await db.Sites.FindAsync(heartbeat.SiteId);
        if (site is not null)
        {
            site.IsOnline = true;
            site.LastSeenUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await registry.UnregisterConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

