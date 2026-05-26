using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using WizAccountant.Contracts;

namespace WizConnector.Service;

public class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<ConnectorSettings> options,
    IStateStore stateStore,
    IJobExecutor jobExecutor) : BackgroundService
{
    private readonly ConnectorSettings _settings = options.Value;
    private HubConnection? _hub;
    private ConnectorState? _state;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _state = await EnsurePairedAsync(stoppingToken);
        if (_state is null)
        {
            logger.LogError("Connector is not paired. Set Connector:PairingCode and restart.");
            return;
        }

        var hubUrl = $"{_settings.ApiBaseUrl.TrimEnd('/')}/hubs/connector";
        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hub.On<RunJobMessage>("RunJob", async job =>
        {
            var (result, error) = await jobExecutor.ExecuteAsync(job.Operation, job.Parameters, stoppingToken);
            await SubmitResultAsync(job.JobId, result, error, stoppingToken);
        });

        await _hub.StartAsync(stoppingToken);
        await _hub.InvokeAsync("RegisterSite", _state.SiteId, cancellationToken: stoppingToken);
        logger.LogInformation("Connected to hub for SiteId={SiteId}", _state.SiteId);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _hub.InvokeAsync("Heartbeat", new ConnectorHeartbeat
            {
                SiteId = _state.SiteId,
                ConnectorVersion = _settings.ConnectorVersion,
                EvolutionVersion = "NotConnectedYet",
                CompanyName = _state.SiteName
            }, stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task<ConnectorState?> EnsurePairedAsync(CancellationToken ct)
    {
        var existing = await stateStore.ReadAsync(ct);
        if (existing is not null) return existing;
        if (string.IsNullOrWhiteSpace(_settings.PairingCode)) return null;

        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_settings.ApiBaseUrl.TrimEnd('/') + "/");

        var response = await client.PostAsJsonAsync("api/sites/pair", new PairSiteRequest
        {
            PairingCode = _settings.PairingCode,
            DeviceId = _settings.DeviceId,
            ConnectorVersion = _settings.ConnectorVersion
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Pairing failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        var paired = await response.Content.ReadFromJsonAsync<PairSiteResponse>(cancellationToken: ct);
        if (paired is null) return null;

        var state = new ConnectorState
        {
            SiteId = paired.SiteId,
            TenantId = paired.TenantId,
            SiteName = paired.SiteName
        };
        await stateStore.WriteAsync(state, ct);
        logger.LogInformation("Paired successfully. SiteId={SiteId}", state.SiteId);
        return state;
    }

    private async Task SubmitResultAsync(Guid jobId, string? result, string? error, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_settings.ApiBaseUrl.TrimEnd('/') + "/");
        var response = await client.PostAsJsonAsync($"api/jobs/{jobId}/result", new SubmitJobResultRequest
        {
            ResultJson = result,
            Error = error
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Result submit failed for JobId={JobId} status={Status}", jobId, response.StatusCode);
        }
    }
}
