using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

internal static class SageSdkPhase2Handlers
{
    public static string? TryExecute(string operation, Dictionary<string, string> parameters) =>
        operation.ToLowerInvariant() switch
        {
            "supplier.get" => SupplierGet(parameters),
            "customertransaction.get" => CustomerTransactionGet(parameters),
            "customer.openitems" => CustomerOpenItems(parameters),
            "supplier.openitems" => SupplierOpenItems(parameters),
            "gltransaction.list" => GlTransactionList(parameters),
            "salesorder.list" => SalesOrderList(parameters),
            "purchaseorder.list" => PurchaseOrderList(parameters),
            "inventoryitem.list" => InventoryItemList(parameters),
            "inventoryitem.get" => InventoryItemGet(parameters),
            "project.list" => ProjectList(parameters),
            "warehouse.list" => WarehouseList(parameters),
            "taxrate.list" => TaxRateList(parameters),
            "transactioncode.list" => TransactionCodeList(parameters),
            "search.global" => SearchGlobal(parameters),
            "dashboard.summary" => DashboardSummary(parameters),
            "site.diagnostics" => SiteDiagnostics(),
            _ => null
        };

    private static string SupplierGet(Dictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Parameter 'code' is required.");

        var supplier = new Supplier(code);
        return JsonSerializer.Serialize(new
        {
            code = supplier.Code,
            name = supplier.Description,
            email = supplier.EmailAddress,
            telephone = supplier.Telephone
        });
    }

    private static string CustomerTransactionGet(Dictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("autoIdx", out var raw) || !long.TryParse(raw, out var autoIdx))
            throw new ArgumentException("Parameter 'autoIdx' is required.");

        var tx = new CustomerTransaction(autoIdx);
        return JsonSerializer.Serialize(new
        {
            autoIdx,
            account = tx.Account?.Code ?? tx.Customer?.Code,
            reference = tx.Reference,
            description = tx.Description,
            debit = tx.Debit,
            credit = tx.Credit,
            outstanding = tx.Outstanding,
            txDate = tx.Date
        });
    }

    private static string CustomerOpenItems(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "Outstanding <> 0");
        var table = CustomerTransaction.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            autoIdx = SageListHelpers.Col(row, "AutoIdx", "ID"),
            account = SageListHelpers.Col(row, "Account"),
            reference = SageListHelpers.Col(row, "Reference"),
            outstanding = SageListHelpers.Col(row, "Outstanding"),
            txDate = SageListHelpers.Col(row, "TxDate")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string SupplierOpenItems(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "Outstanding <> 0");
        var table = SupplierTransaction.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            autoIdx = SageListHelpers.Col(row, "AutoIdx", "ID"),
            account = SageListHelpers.Col(row, "Account"),
            reference = SageListHelpers.Col(row, "Reference"),
            outstanding = SageListHelpers.Col(row, "Outstanding"),
            txDate = SageListHelpers.Col(row, "TxDate")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string GlTransactionList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "AutoIdx > 0");
        var table = GLTransaction.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            autoIdx = SageListHelpers.Col(row, "AutoIdx", "ID"),
            account = SageListHelpers.Col(row, "Account"),
            reference = SageListHelpers.Col(row, "Reference"),
            description = SageListHelpers.Col(row, "Description"),
            debit = SageListHelpers.Col(row, "Debit"),
            credit = SageListHelpers.Col(row, "Credit"),
            txDate = SageListHelpers.Col(row, "TxDate")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string SalesOrderList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "1=1");
        var table = SalesOrder.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            orderNo = SageListHelpers.Col(row, "OrderNum", "OrderNo"),
            account = SageListHelpers.Col(row, "Account"),
            orderDate = SageListHelpers.Col(row, "OrderDate"),
            status = SageListHelpers.Col(row, "DocState", "Status")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string PurchaseOrderList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "1=1");
        var table = PurchaseOrder.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            orderNo = SageListHelpers.Col(row, "OrderNum", "OrderNo"),
            account = SageListHelpers.Col(row, "Account"),
            orderDate = SageListHelpers.Col(row, "OrderDate"),
            status = SageListHelpers.Col(row, "DocState", "Status")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string InventoryItemList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "1=1");
        var table = InventoryItem.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            code = SageListHelpers.Col(row, "Code", "ItemCode"),
            description = SageListHelpers.Col(row, "Description"),
            qtyOnHand = SageListHelpers.Col(row, "QtyOnHand", "Quantity")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string InventoryItemGet(Dictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Parameter 'code' is required.");

        var item = new InventoryItem(code);
        return JsonSerializer.Serialize(new
        {
            code = item.Code,
            description = item.Description
        });
    }

    private static string ProjectList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "1=1");
        var table = Project.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            code = SageListHelpers.Col(row, "ProjectCode", "Code"),
            name = SageListHelpers.Col(row, "Name", "Description")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string WarehouseList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "1=1");
        var table = Warehouse.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            code = SageListHelpers.Col(row, "Code"),
            name = SageListHelpers.Col(row, "Name", "Description")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string TaxRateList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "1=1");
        var table = TaxRate.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            code = SageListHelpers.Col(row, "Code"),
            description = SageListHelpers.Col(row, "Description"),
            rate = SageListHelpers.Col(row, "TaxRate", "Rate")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string TransactionCodeList(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "1=1");
        var table = TransactionCode.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            code = SageListHelpers.Col(row, "Code"),
            description = SageListHelpers.Col(row, "Description")
        });
        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static string SearchGlobal(Dictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Parameter 'query' is required.");

        var q = SageListHelpers.SanitizeSearchQuery(query);
        var like = $"%{q}%";
        var customerCriteria = $"Account LIKE '{like}' OR Name LIKE '{like}'";
        var supplierCriteria = customerCriteria;
        var glCriteria = $"Account LIKE '{like}' OR Description LIKE '{like}'";

        var customers = SageListHelpers.MapRows(Customer.List(customerCriteria), r => new
        {
            type = "customer",
            code = SageListHelpers.Col(r, "Account"),
            name = SageListHelpers.Col(r, "Name")
        });
        var suppliers = SageListHelpers.MapRows(Supplier.List(supplierCriteria), r => new
        {
            type = "supplier",
            code = SageListHelpers.Col(r, "Account"),
            name = SageListHelpers.Col(r, "Name")
        });
        var accounts = SageListHelpers.MapRows(GLAccount.List(glCriteria), r => new
        {
            type = "glaccount",
            code = SageListHelpers.Col(r, "Account"),
            name = SageListHelpers.Col(r, "Description")
        });

        var hits = customers.Concat(suppliers).Concat(accounts).Take(50).ToList();
        return JsonSerializer.Serialize(new { query = q, hits, dataAsOfUtc = DateTimeOffset.UtcNow });
    }

    private static string DashboardSummary(Dictionary<string, string> parameters)
    {
        var customerCount = Customer.List("DCLink > 0")?.Rows.Count ?? 0;
        var supplierCount = Supplier.List("DCLink > 0")?.Rows.Count ?? 0;
        var openAr = TryCount(() => CustomerTransaction.List("Outstanding <> 0"));
        var openAp = TryCount(() => SupplierTransaction.List("Outstanding <> 0"));

        return JsonSerializer.Serialize(new
        {
            dataAsOfUtc = DateTimeOffset.UtcNow,
            kpis = new
            {
                customerCount,
                supplierCount,
                openArItemCount = openAr,
                openApItemCount = openAp
            },
            note = "Phase 2 read-only dashboard; balances are row counts from Sage lists."
        });
    }

    private static string SiteDiagnostics()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WizConnector", "logs");
        var logTail = Array.Empty<string>();
        if (Directory.Exists(logDir))
        {
            var latest = Directory.GetFiles(logDir, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (latest is not null)
                logTail = File.ReadLines(latest).TakeLast(20).ToArray();
        }

        return JsonSerializer.Serialize(new
        {
            connectorVersion = typeof(SageSdkJobExecutor).Assembly.GetName().Version?.ToString(),
            sdkVersion = typeof(DatabaseContext).Assembly.GetName().Version?.ToString(),
            sageConfigPresent = File.Exists(WizAccountant.Contracts.ConnectorPaths.SageConfigFilePath),
            logTail,
            timestampUtc = DateTimeOffset.UtcNow
        });
    }

    private static int TryCount(Func<DataTable?> list)
    {
        try { return list()?.Rows.Count ?? 0; }
        catch { return -1; }
    }
}
