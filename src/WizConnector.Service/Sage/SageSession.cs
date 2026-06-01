using Microsoft.Extensions.Options;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Thread-safe Evolution SDK connection scope (one company DB per connector instance).
/// </summary>
public sealed class SageSession
{
    private readonly SageSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SageSession(IOptions<SageSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<T> RunAsync<T>(Func<T> action, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    Connect();
                    return action();
                }
                catch (Exception ex) when (attempt == 0 && IsConnectionError(ex))
                {
                    // Evolution SQL connection can drop between jobs — retry once with a fresh connect.
                }
            }

            throw new InvalidOperationException("Sage connection failed after retry.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Same sequence as WizConnector.Setup “Test Sage connection”.</summary>
    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(_settings.CommonConnectionString))
            throw new InvalidOperationException(
                "Sage common database is not configured. In WizPilot click Open Sage setup, select the common database (e.g. SageCommon11), Test Sage connection, then Save.");

        if (string.IsNullOrWhiteSpace(_settings.CompanyConnectionString))
            throw new InvalidOperationException(
                "Sage company database is not configured. Open Sage setup and save your company database.");

        if (string.IsNullOrWhiteSpace(_settings.LicenseSerial) || string.IsNullOrWhiteSpace(_settings.LicenseKey))
            throw new InvalidOperationException(
                "Sage SDK licence is not configured. Open Sage setup and enter licence serial and key.");

        DatabaseContext.CreateCommonDBConnection(_settings.CommonConnectionString);
        DatabaseContext.SetLicense(_settings.LicenseSerial, _settings.LicenseKey);
        DatabaseContext.CreateConnection(_settings.CompanyConnectionString);

        if (_settings.BranchId > 0)
            DatabaseContext.SetBranchContext(_settings.BranchId);

        if (!string.IsNullOrWhiteSpace(_settings.AgentUser))
        {
            if (!string.IsNullOrWhiteSpace(_settings.AgentPassword))
            {
                if (!Agent.Authenticate(_settings.AgentUser, _settings.AgentPassword))
                    throw new InvalidOperationException(
                        $"Sage agent authentication failed for user '{_settings.AgentUser}'. Check agent password in Sage setup.");
            }

            DatabaseContext.CurrentAgent = new Agent(_settings.AgentUser);
        }
    }

    private static bool IsConnectionError(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("ADP_ConnectionRequired", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
