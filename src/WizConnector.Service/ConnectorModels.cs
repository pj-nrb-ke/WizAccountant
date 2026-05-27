using System.Text.Json;

namespace WizConnector.Service;

public sealed class ConnectorSettings
{
    public string ApiBaseUrl { get; set; } = "https://localhost:5001";
    public string PairingCode { get; set; } = string.Empty;
    public string DeviceId { get; set; } = Environment.MachineName;
    public string ConnectorVersion { get; set; } = "0.1.0";
    /// <summary>P1-26: poll for jobs via REST when SignalR is disconnected.</summary>
    public bool RestJobPollEnabled { get; set; } = true;
    public int RestPollWaitSeconds { get; set; } = 30;
}

public sealed class ConnectorState
{
    public Guid SiteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
}

public interface IStateStore
{
    Task<ConnectorState?> ReadAsync(CancellationToken ct);
    Task WriteAsync(ConnectorState state, CancellationToken ct);
}

public sealed class FileStateStore : IStateStore
{
    private static readonly string FilePath = WizAccountant.Contracts.ConnectorPaths.StateFilePath;

    public async Task<ConnectorState?> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath)) return null;
        var json = await File.ReadAllTextAsync(FilePath, ct);
        return JsonSerializer.Deserialize<ConnectorState>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task WriteAsync(ConnectorState state, CancellationToken ct)
    {
        Directory.CreateDirectory(WizAccountant.Contracts.ConnectorPaths.ConfigFolder);
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(FilePath, json, ct);
    }
}

public interface IJobExecutor
{
    Task<(string? resultJson, string? error)> ExecuteAsync(string operation, Dictionary<string, string> parameters, CancellationToken ct);
}

public sealed class MockJobExecutor(ILogger<MockJobExecutor> logger) : IJobExecutor
{
    public Task<(string? resultJson, string? error)> ExecuteAsync(string operation, Dictionary<string, string> parameters, CancellationToken ct)
    {
        // Phase 1 read handlers shell only (SDK hookup in next iteration).
        if (string.Equals(operation, "Site.Health", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                ok = true,
                source = "WizConnector",
                timestampUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, "Customer.List", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                items = new[]
                {
                    new { code = "DEMO001", name = "Demo Customer 1" },
                    new { code = "DEMO002", name = "Demo Customer 2" }
                },
                note = "Phase 1 mock output."
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (operation.Equals("customertransaction.list", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                items = new[] { new { autoIdx = "1", account = "DEMO001", reference = "INV-001", debit = "100.00" } },
                criteria = parameters.GetValueOrDefault("criteria") ?? "mock",
                note = "Phase 1 mock output."
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (operation.Equals("supplier.list", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                items = new[] { new { code = "SUPP001", name = "Demo Supplier" } },
                note = "Phase 1 mock output."
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (operation.Equals("suppliertransaction.list", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                items = new[] { new { autoIdx = "1", account = "SUPP001", reference = "PO-001", credit = "50.00" } },
                note = "Phase 1 mock output."
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        logger.LogWarning("Unsupported operation requested: {Operation}", operation);
        return Task.FromResult<(string?, string?)>((null, $"Unsupported operation: {operation}"));
    }
}

