using System.Text.Json;
using WizConnector.Service.Sage;

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
    /// <summary>P3: allow write handlers (default false — enable only after pilot sign-off).</summary>
    public bool WritesEnabled { get; set; }
    public bool WriteConsentRequired { get; set; } = true;
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

        if (operation == "dashboard.summary")
        {
            var payload = JsonSerializer.Serialize(new
            {
                dataAsOfUtc = DateTimeOffset.UtcNow,
                kpis = new { customerCount = 2, supplierCount = 1, openArItemCount = 3, openApItemCount = 1 },
                note = "Phase 2 mock dashboard"
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (operation == "search.global")
        {
            var payload = JsonSerializer.Serialize(new
            {
                query = "mock",
                hits = new[] { new { type = "customer", code = "DEMO001", name = "Demo Customer" } },
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, "salesinvoice.discount.count", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = "SAGE-SALES-INV-DISC-COUNT-001",
                year = 2026,
                invoiceCount = 12,
                countOnly = true,
                finding = "Mock: 12 sales invoices in 2026 with discounts.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, "salescreditnote.count", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = "SAGE-AR-CREDIT-NOTE-COUNT-001",
                dateFrom = "2025-01-01",
                dateTo = "2025-03-31",
                periodLabel = "Q1 2025",
                creditNoteCount = 7,
                totalValue = 125000m,
                countOnly = true,
                aggregationMode = true,
                finding = "Mock: 7 sales credit notes in Q1 2025.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, "customer.credit.balances", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = "SAGE-AR-CREDIT-BAL-001",
                hasCreditBalances = true,
                customers = new[] { new { rank = 1, code = "C001", name = "Credit Customer", balance = -5000m } },
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, "supplier.aged.top", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = "SAGE-AP-AGED-TOP-001",
                requestedTop = 5,
                topSuppliers = new[] { new { rank = 1, code = "S001", name = "Demo Supplier", totalOutstanding = 10000m, daysOutstanding = 120, oldestInvoiceDate = "2025-01-01" } },
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, "customer.aged.top", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = "SAGE-AR-AGED-TOP-001",
                requestedTop = 5,
                topCustomers = new[]
                {
                    new { rank = 1, code = "DEMO01", name = "Demo Debtor Ltd", totalOutstanding = 1_245_000m, oldestInvoiceDate = "2024-04-15", daysOutstanding = 412, openLineCount = 3 }
                },
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (operation is "customer.collections.summary"
            or "customer.collections.by.month"
            or "customer.collections.by.customer"
            or "customer.collections.top")
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = "SAGE-AR-COLLECTIONS-001",
                operation,
                dateFrom = "2025-04-01",
                dateTo = "2025-06-30",
                totalCollections = 125000m,
                monthlyBreakdown = new[]
                {
                    new { year = 2025, monthNo = 4, month = "April", collectionAmount = 40000m },
                    new { year = 2025, monthNo = 5, month = "May", collectionAmount = 45000m },
                    new { year = 2025, monthNo = 6, month = "June", collectionAmount = 40000m }
                },
                byCustomer = new[]
                {
                    new { rank = 1, customerCode = "A001", customerName = "Demo Customer", collectionAmount = 50000m }
                },
                note = "Phase 2 mock — rebuild connector for live PostAR collections.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, "inventory.bs.negative_ledgers", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = "SAGE-BS-STOCK-NEGATIVE-001",
                asOfDate = DateTime.Today.ToString("yyyy-MM-dd"),
                hasNegativeLedgers = true,
                totalNegativeStockValue = -15_000m,
                ledgerCount = 1,
                ledgers = new[]
                {
                    new { glAccount = "050", glAccountName = "Inventory - WIP (credit)", netBalance = -15_000m }
                },
                largestNegative = new { glAccount = "050", glAccountName = "Inventory - WIP (credit)", netBalance = -15_000m },
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, "inventory.gl.reconcile", StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = "SAGE-INVVAL-RECON-CANONICAL-001",
                asOfDate = DateTime.Today.ToString("yyyy-MM-dd"),
                balanceSheetStockValue = 41_700_394.03m,
                inventoryValuation = 42_083_171.63m,
                difference = 382_777.60m,
                matches = false,
                reliableResult = true,
                sanityCheckPassed = true,
                executedSqlValuation = true,
                usedSdkFallback = false,
                valuationLineCount = 120,
                valuationAccountCount = 4,
                glAccountCount = 4,
                detailTotalsMatchGrandTotal = true,
                finding = "Mock: Inventory valuation is not matching Balance Sheet stock value.",
                accounts = new[]
                {
                    new { rowType = "DETAIL", glAccount = "1225", glAccountName = "Inventory - Packaging", balanceSheet = 11_737_848.06m, inventoryValuation = 12_000_000m, difference = 262_151.94m, match = "No" }
                },
                note = "Rebuild connector for live Sage SQL reconciliation.",
                dataAsOfUtc = DateTimeOffset.UtcNow,
                executedAtUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, SupplierUnpaidHandlers.CountOperation, StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = SupplierUnpaidHandlers.CountQuerySerial,
                operation = SupplierUnpaidHandlers.CountOperation,
                countOnly = true,
                aggregationMode = true,
                asOfDate = DateTime.Today.ToString("yyyy-MM-dd"),
                totalUnpaidSuppliers = 12,
                totalOutstandingPayable = 458_920.55m,
                suppliersWithUnpaidInvoices = 12,
                finding = "Total unpaid suppliers as of today: 12.",
                note = "Phase 2 mock — rebuild connector for live Sage AP open balances.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, SupplierUnpaidHandlers.ListOperation, StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = SupplierUnpaidHandlers.ListQuerySerial,
                operation = SupplierUnpaidHandlers.ListOperation,
                asOfDate = DateTime.Today.ToString("yyyy-MM-dd"),
                totalUnpaidSuppliers = 2,
                totalOutstandingPayable = 125_000m,
                suppliers = new[]
                {
                    new { code = "SUPP001", name = "Demo Supplier 1", invoiceCount = 3, totalOutstanding = 80_000m },
                    new { code = "SUPP002", name = "Demo Supplier 2", invoiceCount = 1, totalOutstanding = 45_000m }
                },
                finding = "2 supplier(s) with unpaid AP balances as of today.",
                note = "Phase 2 mock — rebuild connector for live Sage AP open balances.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (string.Equals(operation, SupplierUnpaidHandlers.TopOperation, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operation, SupplierUnpaidHandlers.SummaryOperation, StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Serialize(new
            {
                querySerial = SupplierUnpaidHandlers.TopQuerySerial,
                operation = SupplierUnpaidHandlers.TopOperation,
                asOfDate = DateTime.Today.ToString("yyyy-MM-dd"),
                totalUnpaidSuppliers = 2,
                totalOutstandingPayable = 125_000m,
                topSuppliers = new[]
                {
                    new { rank = 1, code = "SUPP001", name = "Demo Supplier 1", invoiceCount = 3, totalOutstanding = 80_000m }
                },
                finding = "Top 1 supplier(s) by outstanding AP balance as of today.",
                note = "Phase 2 mock — rebuild connector for live Sage AP open balances.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (operation is "site.diagnostics"
            or "customer.openitems" or "customer.unpaid.summary"
            or "supplier.openitems" or "gltransaction.list"
            or "salesorder.list" or "purchaseorder.list" or "inventoryitem.list"
            or "project.list" or "warehouse.list" or "taxrate.list"
            or "salesrepresentative.list" or "settlementterms.list" or "orderstatus.list"
            or "priority.list" or "currency.list"
            or "customer.address" or "inventoryitem.stock.qty" or "inventoryitem.sellingprice"
            or "inventoryitem.salestax"
            or "inventoryitem.units"
            or "salesorder.nextnumber"
            or "transactioncode.list")
        {
            var payload = JsonSerializer.Serialize(new
            {
                items = Array.Empty<object>(),
                total = 0,
                note = $"Phase 2 mock for {operation}",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
            return Task.FromResult<(string?, string?)>((payload, null));
        }

        if (ConnectorWriteAllowlist.IsWrite(operation))
        {
            var payload = parameters.GetValueOrDefault("payload") ?? "{}";
            var simulated = JsonSerializer.Serialize(new
            {
                simulated = true,
                operation,
                ok = true,
                message = "Phase 3 mock write — enable Sage and Connector:WritesEnabled for live post.",
                payloadLength = payload.Length
            });
            return Task.FromResult<(string?, string?)>((simulated, null));
        }

        logger.LogWarning("Unsupported operation requested: {Operation}", operation);
        return Task.FromResult<(string?, string?)>((null, $"Unsupported operation: {operation}"));
    }
}

