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
  private bool _initialized;

  public SageSession(IOptions<SageSettings> settings)
  {
    _settings = settings.Value;
  }

  public async Task<T> RunAsync<T>(Func<T> action, CancellationToken ct)
  {
    await _gate.WaitAsync(ct);
    try
    {
      EnsureConnected();
      return action();
    }
    finally
    {
      _gate.Release();
    }
  }

  private void EnsureConnected()
  {
    if (_initialized) return;

    if (string.IsNullOrWhiteSpace(_settings.CompanyConnectionString))
      throw new InvalidOperationException("Sage:CompanyConnectionString is not configured.");

    if (!string.IsNullOrWhiteSpace(_settings.CommonConnectionString))
      DatabaseContext.CreateCommonDBConnection(_settings.CommonConnectionString);

    if (!string.IsNullOrWhiteSpace(_settings.LicenseSerial) && !string.IsNullOrWhiteSpace(_settings.LicenseKey))
      DatabaseContext.SetLicense(_settings.LicenseSerial, _settings.LicenseKey);

    DatabaseContext.CreateConnection(_settings.CompanyConnectionString);

    if (_settings.BranchId > 0)
      DatabaseContext.SetBranchContext(_settings.BranchId);

    if (!string.IsNullOrWhiteSpace(_settings.AgentUser))
    {
      if (!string.IsNullOrWhiteSpace(_settings.AgentPassword))
      {
        if (!Agent.Authenticate(_settings.AgentUser, _settings.AgentPassword))
          throw new InvalidOperationException($"Sage agent authentication failed for user '{_settings.AgentUser}'.");
      }

      DatabaseContext.CurrentAgent = new Agent(_settings.AgentUser);
    }

    _initialized = true;
  }
}
