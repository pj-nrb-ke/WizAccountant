using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

public sealed class SageSdkJobExecutor(ILogger<SageSdkJobExecutor> logger, SageSession session) : IJobExecutor
{
  public async Task<(string? resultJson, string? error)> ExecuteAsync(
    string operation,
    Dictionary<string, string> parameters,
    CancellationToken ct)
  {
    try
    {
      var payload = await session.RunAsync(() => ExecuteCore(operation, parameters), ct);
      return (payload, null);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Sage SDK operation {Operation} failed", operation);
      return (null, ex.Message);
    }
  }

  private static string ExecuteCore(string operation, Dictionary<string, string> parameters)
  {
    return operation.ToLowerInvariant() switch
    {
      "site.health" => SiteHealth(),
      "customer.list" => CustomerList(parameters),
      "customer.get" => CustomerGet(parameters),
      "glaccount.list" => GlAccountList(parameters),
      "glaccount.get" => GlAccountGet(parameters),
      _ => throw new NotSupportedException($"Unsupported operation: {operation}")
    };
  }

  private static string SiteHealth()
  {
    var table = Customer.List("DCLink > 0");
    var count = table?.Rows.Count ?? 0;
    return JsonSerializer.Serialize(new
    {
      ok = true,
      source = "Pastel.Evolution",
      sdkVersion = typeof(DatabaseContext).Assembly.GetName().Version?.ToString(),
      customerCountSample = count,
      timestampUtc = DateTimeOffset.UtcNow
    });
  }

  private static string CustomerList(Dictionary<string, string> parameters)
  {
    var criteria = parameters.TryGetValue("criteria", out var c) && !string.IsNullOrWhiteSpace(c)
      ? c
      : "DCLink > 0";

    var table = Customer.List(criteria);
    var items = ToRows(table, row => new
    {
      code = row["Account"]?.ToString(),
      name = row["Name"]?.ToString(),
      dclink = row["DCLink"]?.ToString()
    });

    return JsonSerializer.Serialize(new { items, criteria });
  }

  private static string CustomerGet(Dictionary<string, string> parameters)
  {
    if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
      throw new ArgumentException("Parameter 'code' is required.");

    var customer = new Customer(code);
    return JsonSerializer.Serialize(new
    {
      code = customer.Code,
      name = customer.Description,
      email = customer.EmailAddress,
      telephone = customer.Telephone
    });
  }

  private static string GlAccountList(Dictionary<string, string> parameters)
  {
    var criteria = parameters.TryGetValue("criteria", out var c) && !string.IsNullOrWhiteSpace(c)
      ? c
      : "ActiveAccount = 1";

    var table = GLAccount.List(criteria);
    var items = ToRows(table, row => new
    {
      code = row["Account"]?.ToString(),
      description = row["Description"]?.ToString(),
      accountLink = row["AccountLink"]?.ToString()
    });

    return JsonSerializer.Serialize(new { items, criteria });
  }

  private static string GlAccountGet(Dictionary<string, string> parameters)
  {
    if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
      throw new ArgumentException("Parameter 'code' is required.");

    var account = new GLAccount(code);
    return JsonSerializer.Serialize(new
    {
      code = account.Code,
      description = account.Description,
      active = account.Active
    });
  }

  private static List<T> ToRows<T>(DataTable? table, Func<DataRow, T> map)
  {
    var list = new List<T>();
    if (table is null) return list;
    foreach (DataRow row in table.Rows)
      list.Add(map(row));
    return list;
  }
}
