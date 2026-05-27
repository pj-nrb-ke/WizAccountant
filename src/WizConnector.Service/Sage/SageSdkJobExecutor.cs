using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pastel.Evolution;
using WizConnector.Service;

namespace WizConnector.Service.Sage;

public sealed class SageSdkJobExecutor(
    ILogger<SageSdkJobExecutor> logger,
    SageSession session,
    Microsoft.Extensions.Options.IOptions<ConnectorSettings> connectorOptions) : IJobExecutor
{
    private readonly IdempotencyStore _idempotency = new();
    private readonly WriteConsentStore _consent = new();
    private readonly ConnectorSettings _connector = connectorOptions.Value;
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

    private string ExecuteCore(string operation, Dictionary<string, string> parameters)
    {
        var write = SageSdkWriteHandlers.TryExecute(
            operation, parameters, _connector.WritesEnabled, _connector.WriteConsentRequired, _consent, _idempotency);
        if (write is not null) return write;

        var phase2 = SageSdkPhase2Handlers.TryExecute(operation, parameters);
        if (phase2 is not null) return phase2;

        return operation.ToLowerInvariant() switch
        {
            "site.health" => SiteHealth(),
            "customer.list" => CustomerList(parameters),
            "customer.get" => CustomerGet(parameters),
            "customertransaction.list" => CustomerTransactionList(parameters),
            "supplier.list" => SupplierList(parameters),
            "suppliertransaction.list" => SupplierTransactionList(parameters),
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
        var criteria = SageListHelpers.BuildCriteria(parameters, "DCLink > 0");
        var table = Customer.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            code = SageListHelpers.Col(row, "Account"),
            name = SageListHelpers.Col(row, "Name"),
            dclink = SageListHelpers.Col(row, "DCLink")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
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

    private static string CustomerTransactionList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "AutoIdx > 0");
        var table = CustomerTransaction.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            autoIdx = SageListHelpers.Col(row, "AutoIdx", "ID"),
            account = SageListHelpers.Col(row, "Account"),
            txDate = SageListHelpers.Col(row, "TxDate"),
            reference = SageListHelpers.Col(row, "Reference"),
            description = SageListHelpers.Col(row, "Description"),
            debit = SageListHelpers.Col(row, "Debit"),
            credit = SageListHelpers.Col(row, "Credit")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string SupplierList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "DCLink > 0");
        var table = Supplier.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            code = SageListHelpers.Col(row, "Account"),
            name = SageListHelpers.Col(row, "Name"),
            dclink = SageListHelpers.Col(row, "DCLink")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string SupplierTransactionList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "AutoIdx > 0");
        var table = SupplierTransaction.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            autoIdx = SageListHelpers.Col(row, "AutoIdx", "ID"),
            account = SageListHelpers.Col(row, "Account"),
            txDate = SageListHelpers.Col(row, "TxDate"),
            reference = SageListHelpers.Col(row, "Reference"),
            description = SageListHelpers.Col(row, "Description"),
            debit = SageListHelpers.Col(row, "Debit"),
            credit = SageListHelpers.Col(row, "Credit")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string GlAccountList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "ActiveAccount = 1");
        var table = GLAccount.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            code = SageListHelpers.Col(row, "Account"),
            description = SageListHelpers.Col(row, "Description"),
            accountLink = SageListHelpers.Col(row, "AccountLink")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
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

}
