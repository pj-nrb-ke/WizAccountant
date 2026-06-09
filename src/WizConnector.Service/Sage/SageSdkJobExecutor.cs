using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pastel.Evolution;
using WizConnector.Service;

namespace WizConnector.Service.Sage;

public sealed class SageSdkJobExecutor(
    ILogger<SageSdkJobExecutor> logger,
    SageSession session,
    IOptions<SageSettings> sageSettings,
    IOptions<ConnectorSettings> connectorOptions) : IJobExecutor
{
    private readonly IdempotencyStore _idempotency = new();
    private readonly WriteConsentStore _consent = new();
    private readonly ConnectorSettings _connector = connectorOptions.Value;
    private readonly SageSettings _sage = sageSettings.Value;

    public async Task<(string? resultJson, string? error)> ExecuteAsync(
        string operation,
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        // MC1: site.companies does not require a Sage connection
        if (operation.Equals("site.companies", StringComparison.OrdinalIgnoreCase))
            return (SiteMetadataHandler.ExecuteCompanyList(_sage), null);

        try
        {
            // MC1: pass optional "company" parameter to select the right company DB
            parameters.TryGetValue("company", out var companyAlias);
            var payload = await session.RunAsync(() => ExecuteCore(operation, parameters), companyAlias, ct);
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
        SageSdkPhase2Handlers.Configure(_sage);

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

    private string SiteHealth()
    {
        var table = Customer.List("DCLink > 0");
        var count = table?.Rows.Count ?? 0;
        return JsonSerializer.Serialize(new
        {
            ok = true,
            source = "Pastel.Evolution",
            sdkVersion = typeof(DatabaseContext).Assembly.GetName().Version?.ToString(),
            customerCountSample = count,
            companyDatabase = ParseConnectionCatalog(_sage.CompanyConnectionString),
            commonDatabase = ParseConnectionCatalog(_sage.CommonConnectionString),
            timestampUtc = DateTimeOffset.UtcNow
        });
    }

    private static string? ParseConnectionCatalog(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try
        {
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? null : builder.InitialCatalog;
        }
        catch
        {
            return null;
        }
    }

    private static string CustomerList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "DCLink > 0");
        var table = Customer.List(criteria);
        var minBalance = SageListHelpers.ParseParameterDecimal(parameters, "minBalance");
        var items = new List<CustomerListItem>();

        if (table is not null)
        {
            foreach (DataRow row in table.Rows)
            {
                var code = SageListHelpers.Col(row, "Account") ?? "";
                var balance = ParseRowBalance(row);
                if (!balance.HasValue && !string.IsNullOrWhiteSpace(code))
                    balance = TryGetCustomerBalance(code);

                items.Add(new CustomerListItem(
                    code,
                    SageListHelpers.Col(row, "Name") ?? "",
                    SageListHelpers.Col(row, "DCLink") ?? "",
                    balance));
            }
        }

        string? note = null;
        if (minBalance.HasValue)
        {
            var withBalance = items.Count(i => i.Balance.HasValue);
            items = items.Where(i => i.Balance is not null && i.Balance >= minBalance.Value).ToList();
            note = withBalance == 0
                ? $"Balance filter >= {minBalance.Value} requested, but Sage did not return balances for customers. Try Customers (AR) → Open items, or check Sage setup."
                : $"Filtered to {items.Count} customer(s) with balance >= {minBalance.Value} (read {withBalance} balance(s) from Sage).";
        }

        var dtoItems = items.Select(i => new
        {
            code = i.Code,
            name = i.Name,
            dclink = i.Dclink,
            balance = i.Balance
        }).ToList();

        return SageListHelpers.SerializePaged(dtoItems, criteria, parameters, note, minBalance);
    }

    private static decimal? TryGetCustomerBalance(string code)
    {
        try
        {
            var customer = new Customer(code);
            foreach (var propName in new[] { "Balance", "DCBalance", "AccountBalance", "Outstanding", "CreditLimit" })
            {
                var prop = typeof(Customer).GetProperty(propName);
                if (prop?.GetValue(customer) is decimal d) return d;
                if (prop?.GetValue(customer) is double dbl) return (decimal)dbl;
                if (prop?.GetValue(customer) is float f) return (decimal)f;
            }
        }
        catch
        {
            // Customer code invalid or SDK error — leave balance null.
        }

        return null;
    }

    private static decimal? ParseRowBalance(DataRow row) =>
        SageListHelpers.ParseRowAmount(row, "DCBalance", "Balance", "fAccBal", "AccountBalance", "Outstanding");

    private static string SupplierList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "DCLink > 0");
        var table = Supplier.List(criteria);
        var minBalance = SageListHelpers.ParseParameterDecimal(parameters, "minBalance");
        var items = new List<SupplierListItem>();

        if (table is not null)
        {
            foreach (DataRow row in table.Rows)
            {
                var code = SageListHelpers.Col(row, "Account") ?? "";
                var balance = SageListHelpers.ParseRowAmount(row, "DCBalance", "Balance", "fAccBal", "AccountBalance", "Outstanding");
                if (!balance.HasValue && !string.IsNullOrWhiteSpace(code))
                    balance = TryGetSupplierBalance(code);

                items.Add(new SupplierListItem(
                    code,
                    SageListHelpers.Col(row, "Name") ?? "",
                    SageListHelpers.Col(row, "DCLink") ?? "",
                    balance));
            }
        }

        string? note = null;
        if (minBalance.HasValue)
        {
            var withBalance = items.Count(i => i.Balance.HasValue);
            items = items.Where(i => i.Balance is not null && i.Balance >= minBalance.Value).ToList();
            note = withBalance == 0
                ? $"Balance filter >= {minBalance.Value} requested, but Sage did not return supplier balances."
                : $"Filtered to {items.Count} supplier(s) with balance >= {minBalance.Value} (read {withBalance} balance(s) from Sage).";
        }

        var dtoItems = items.Select(i => new
        {
            code = i.Code,
            name = i.Name,
            dclink = i.Dclink,
            balance = i.Balance
        }).ToList();

        return SageListHelpers.SerializePaged(dtoItems, criteria, parameters, note, minBalance);
    }

    private static decimal? TryGetSupplierBalance(string code)
    {
        try
        {
            var supplier = new Supplier(code);
            return SageListHelpers.TryGetSdkPropertyDecimal(supplier,
                "Balance", "DCBalance", "AccountBalance", "Outstanding", "CreditLimit");
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct CustomerListItem(string Code, string Name, string Dclink, decimal? Balance);
    private readonly record struct SupplierListItem(string Code, string Name, string Dclink, decimal? Balance);

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
