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
    IJobExecutor jobExecutor,
    ConnectorUpdateService updateService) : BackgroundService
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

        // MC4: check for connector updates (non-fatal, fire-and-forget)
        _ = updateService.CheckForUpdateAsync(stoppingToken);

        var hubUrl = $"{_settings.ApiBaseUrl.TrimEnd('/')}/hubs/connector";
        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hub.On<RunJobMessage>("RunJob", job => HandleJobAsync(job, stoppingToken));

        _hub.Reconnecting += _ =>
        {
            logger.LogWarning("SignalR reconnecting; REST job poll will be used if enabled.");
            return Task.CompletedTask;
        };

        _hub.Reconnected += async _ =>
        {
            if (_state is not null)
                await _hub.InvokeAsync("RegisterSite", _state.SiteId, cancellationToken: stoppingToken);
            logger.LogInformation("SignalR reconnected.");
        };

        try
        {
            await _hub.StartAsync(stoppingToken);
            await _hub.InvokeAsync("RegisterSite", _state.SiteId, cancellationToken: stoppingToken);
            logger.LogInformation("Connected to hub for SiteId={SiteId}", _state.SiteId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Initial SignalR connection failed; using REST poll fallback.");
        }

        var heartbeatDue = DateTimeOffset.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_hub.State == HubConnectionState.Connected)
            {
                if (DateTimeOffset.UtcNow >= heartbeatDue)
                {
                    try
                    {
                        await _hub.InvokeAsync("Heartbeat", new ConnectorHeartbeat
                        {
                            SiteId = _state.SiteId,
                            ConnectorVersion = _settings.ConnectorVersion,
                            EvolutionVersion = "Pastel.Evolution",
                            CompanyName = _state.SiteName
                        }, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Heartbeat failed.");
                    }
                    heartbeatDue = DateTimeOffset.UtcNow.AddSeconds(30);
                }

                await Task.Delay(1000, stoppingToken);
            }
            else if (_settings.RestJobPollEnabled)
            {
                await PollAndRunJobsAsync(stoppingToken);
            }
            else
            {
                try
                {
                    if (_hub.State == HubConnectionState.Disconnected)
                        await _hub.StartAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "SignalR reconnect attempt failed.");
                }
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task PollAndRunJobsAsync(CancellationToken ct)
    {
        if (_state is null) return;

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(_settings.RestPollWaitSeconds + 15);

        try
        {
            var url =
                $"api/connector/jobs/next?siteId={_state.SiteId}&deviceId={Uri.EscapeDataString(_settings.DeviceId)}&waitSeconds={_settings.RestPollWaitSeconds}";
            var response = await client.GetAsync(
                new Uri(new Uri(_settings.ApiBaseUrl.TrimEnd('/') + "/"), url), ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("REST job poll failed: {Status}", response.StatusCode);
                await Task.Delay(5000, ct);
                return;
            }

            var poll = await response.Content.ReadFromJsonAsync<ConnectorJobPollResponse>(cancellationToken: ct);
            if (poll?.HasJob == true && poll.Job is not null)
                await HandleJobAsync(poll.Job, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "REST job poll error.");
            await Task.Delay(5000, ct);
        }
    }

    private async Task HandleJobAsync(RunJobMessage job, CancellationToken ct)
    {
        var (result, error) = await jobExecutor.ExecuteAsync(job.Operation, job.Parameters, ct);
        await SubmitResultAsync(job.JobId, result, error, ct);
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
            logger.LogWarning("Result submit failed for JobId={JobId} status={Status}", jobId, response.StatusCode);
    }
}
