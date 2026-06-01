using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

internal static class SageSdkPhase2Handlers
{
    private static SageSettings? _sageSettings;

    public static void Configure(SageSettings settings) => _sageSettings = settings;

    public static string? TryExecute(string operation, Dictionary<string, string> parameters) =>
        operation.ToLowerInvariant() switch
        {
            "supplier.get" => SupplierGet(parameters),
            "customertransaction.get" => CustomerTransactionGet(parameters),
            "customer.openitems" => CustomerOpenItems(parameters),
            "customer.unpaid.summary" => CustomerUnpaidSummary(parameters),
            "customer.aged.top" => CustomerAgedTopHandler.Execute(parameters),
            "customer.credit.balances" => CustomerCreditBalancesHandler.Execute(parameters),
            "supplier.aged.top" => SupplierAgedTopHandler.Execute(parameters),
            "salesinvoice.discount.count" => SalesInvoiceDiscountCountHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "salesinvoice.discount.top" => SalesInvoiceDiscountTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "ar.invoice.overdue.buckets" => ArOverdueAgingBucketHandler.Execute(parameters),
            "customer.over.creditlimit" => CustomerOverCreditLimitHandler.Execute(parameters),
            "salesinvoice.partially.paid" => SalesInvoicePartiallyPaidHandler.Execute(parameters),
            "customer.invoice.unpaid.olderthan" => CustomerInvoiceUnpaidOlderThanHandler.Execute(parameters),
            "customer.outstanding.debit.top" => CustomerOutstandingDebitTopHandler.Execute(parameters),
            "customer.aged.credit.top" => CustomerAgedCreditTopHandler.Execute(parameters),
            "customer.sales.top" => CustomerSalesTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "customer.payment.prompt.top" => CustomerPaymentPromptTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "customer.payment.late.top" => CustomerPaymentLateTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "customer.payment.behavior.summary" => CustomerPaymentBehaviorSummaryHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "customer.payment.detail" => CustomerPaymentDetailHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "supplier.credit.balances" => SupplierCreditBalancesHandler.Execute(parameters),
            "ap.invoice.overdue.count" => ApOverdueInvoiceCountHandler.Execute(parameters),
            "supplier.invoice.unpaid.olderthan" => SupplierInvoiceUnpaidOlderThanHandler.Execute(parameters),
            "supplier.outstanding.top" => SupplierOutstandingTopHandler.Execute(parameters),
            "purchaseinvoice.partially.paid" => PurchaseInvoicePartiallyPaidHandler.Execute(parameters),
            "purchaseinvoice.duplicate" => PurchaseInvoiceDuplicateHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "supplier.payments.top" => SupplierPaymentsTopHandler.Execute(parameters),
            "purchaseinvoice.count" => PurchaseInvoiceCountHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "purchaseinvoice.top" => PurchaseInvoiceTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "purchaseinvoice.discount.count" => PurchaseInvoiceDiscountCountHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "purchaseinvoice.discount.top" => PurchaseInvoiceDiscountTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "supplier.purchases.top" => SupplierPurchasesTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.slow.moving.top" => InventorySlowMovingTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.nonmoving" => InventoryNonMovingHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.negative.qty" => InventoryNegativeQtyHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.negative.valuation" => InventoryNegativeValuationHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.below.reorder" => InventoryBelowReorderHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.overstocked" => InventoryOverstockedHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.value.top" => InventoryValueTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.movement.top" => InventoryMovementTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "warehouse.value.summary" => WarehouseValueSummaryHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "warehouse.negative.qty" => WarehouseNegativeQtyHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "warehouse.nonmoving" => WarehouseNonMovingHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "warehouse.transfer.summary" => WarehouseTransferSummaryHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "warehouse.discrepancy" => WarehouseDiscrepancyHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.expense.top" => GlExpenseTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.expense.trend" => GlExpenseTrendHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.expense.variance" => GlExpenseVarianceHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.journal.manual" => GlManualJournalsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.journal.users.top" => GlJournalUsersTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.transaction.backdated" => GlBackdatedTransactionsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.balance.unusual" => GlUnusualBalancesHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.journal.round" => GlRoundJournalsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.journal.periodend" => GlPeriodEndJournalsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.journal.duplicate" => GlDuplicateJournalsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.ratios" => GlFinancialRatiosHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "gl.trialbalance.anomaly" => GlTrialBalanceAnomalyHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.adjustment.top" => InventoryAdjustmentTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.summary" => VatSummaryHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.output" => VatOutputHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.input" => VatInputHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.payable.estimate" => VatPayableEstimateHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.trend" => VatTrendHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.anomalies" => VatAnomaliesHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.zero.rated" => VatZeroRatedHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.by.account.top" => VatByAccountTopHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.reconcile" => VatReconcileHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "vat.missing" => VatMissingHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "bank.cashbook" => BankCashbookHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "bank.unusual" => BankUnusualHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "bank.daily.cash" => BankDailyCashHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "treasury.dashboard" => TreasuryDashboardHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "treasury.cash.forecast" => TreasuryCashForecastHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "treasury.collections.forecast" => TreasuryCollectionsForecastHandler.Execute(parameters),
            "treasury.payments.forecast" => TreasuryPaymentsForecastHandler.Execute(parameters),
            "treasury.netcashflow.forecast" => TreasuryCashForecastHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "supplier.openitems" => SupplierOpenItems(parameters),
            "inventory.gl.reconcile" => InventoryGlReconcileHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.warehouse.reconcile" => InventoryWarehouseReconcileHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.stockgroup.reconcile" => InventoryStockGroupReconcileHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.gl.explain" => InventoryGlExplainHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.item.drilldown" => InventoryItemDrilldownHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "inventory.bs.negative_ledgers" => InventoryBsNegativeLedgersHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "ar.gl.reconcile" => ArGlReconcileHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "ar.variance.contributors" => ArVarianceContributorsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "ar.unallocated" => ArUnallocatedHandler.Execute(parameters),
            "ap.gl.reconcile" => ApGlReconcileHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "ap.variance.contributors" => ApVarianceContributorsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "ap.unallocated" => ApUnallocatedHandler.Execute(parameters),
            "vat.variance.contributors" => VatVarianceContributorsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "bank.reconcile.variance" => BankReconcileVarianceHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "bank.deposits.outstanding" => BankOutstandingDepositsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "bank.cheques.unpresented" => BankUnpresentedChequesHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "bank.unmatched" => BankUnmatchedHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "fa.depreciation.reconcile" => FaDepreciationReconcileHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
            "fa.variance.contributors" => FaVarianceContributorsHandler.Execute(
                _sageSettings?.CompanyConnectionString ?? "", parameters),
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
        var dclinkMap = SageCustomerRowResolver.LoadDclinkToAccountMap();
        var today = DateTime.Today;
        var minDays = parameters.TryGetValue("minDaysOutstanding", out var mdRaw) &&
                      int.TryParse(mdRaw, out var minDaysVal)
            ? minDaysVal
            : 0;

        var items = SageListHelpers.MapRows(table, row => new
        {
            autoIdx = SageListHelpers.Col(row, "AutoIdx", "ID"),
            account = SageCustomerRowResolver.ResolveCustomerCode(row, dclinkMap),
            reference = SageListHelpers.Col(row, "Reference"),
            description = SageListHelpers.Col(row, "Description"),
            txType = SageListHelpers.Col(row, "Id", "TrCodeID"),
            debit = SageListHelpers.ParseRowAmount(row, "Debit"),
            credit = SageListHelpers.ParseRowAmount(row, "Credit"),
            outstanding = ResolveOutstanding(row),
            txDate = SageListHelpers.Col(row, "TxDate", "Date")
        });

        if (minDays > 0)
        {
            items = items.Where(x =>
            {
                if (string.IsNullOrWhiteSpace(x.txDate) || !DateTime.TryParse(x.txDate, out var dt))
                    return false;
                return (today - dt.Date).Days >= minDays;
            }).ToList();
        }

        return SageListHelpers.SerializePaged(items, criteria, parameters);
    }

    private static decimal? ResolveOutstanding(DataRow row)
    {
        var direct = SageListHelpers.ParseRowAmount(row, "Outstanding", "fOutstanding", "OutstandingForeign");
        if (direct.HasValue) return direct;
        var debit = SageListHelpers.ParseRowAmount(row, "Debit");
        var credit = SageListHelpers.ParseRowAmount(row, "Credit");
        if (debit is null && credit is null) return null;
        return Math.Abs((debit ?? 0m) - (credit ?? 0m));
    }

    private static string CustomerUnpaidSummary(Dictionary<string, string> parameters)
    {
        var limit = Math.Clamp(SageListHelpers.ParseIntParam(parameters, "top", 15), 1, 50);
        var names = LoadCustomerNameLookup();
        var dclinkMap = SageCustomerRowResolver.LoadDclinkToAccountMap();
        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null)
        {
            return JsonSerializer.Serialize(new
            {
                topCustomers = Array.Empty<object>(),
                totalOpenLines = 0,
                customersWithUnpaidInvoices = 0,
                unallocatedLines = 0,
                note = "No open AR data returned from Sage.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
        }

        var buckets = new Dictionary<string, CustomerUnpaidBucket>(StringComparer.OrdinalIgnoreCase);
        var totalLines = 0;
        var unallocated = 0;
        var skippedNonInvoice = 0;

        foreach (DataRow row in table.Rows)
        {
            totalLines++;
            if (!SageCustomerRowResolver.IsOpenInvoiceOrOrderLine(row))
            {
                skippedNonInvoice++;
                continue;
            }

            var account = SageCustomerRowResolver.ResolveCustomerCode(row, dclinkMap);
            if (string.IsNullOrWhiteSpace(account))
            {
                unallocated++;
                continue;
            }

            var outstanding = ResolveOutstanding(row);
            if (outstanding is null or <= 0) continue;

            if (!buckets.TryGetValue(account, out var bucket))
            {
                buckets[account] = bucket = new CustomerUnpaidBucket(account, ResolveCustomerName(account, names));
            }

            bucket.InvoiceCount++;
            bucket.TotalOutstanding += outstanding.Value;
        }

        var ranked = buckets.Values
            .OrderByDescending(b => b.TotalOutstanding)
            .ThenByDescending(b => b.InvoiceCount)
            .Take(limit)
            .Select(b => new
            {
                code = b.Code,
                name = b.Name,
                invoiceCount = b.InvoiceCount,
                totalOutstanding = b.TotalOutstanding
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            topCustomers = ranked,
            totalOpenLines = totalLines,
            customersWithUnpaidInvoices = buckets.Count,
            unallocatedLines = unallocated,
            skippedNonInvoiceLines = skippedNonInvoice,
            note = "Ranked by customer: sum of open invoice/sales-order lines (Outstanding > 0). Excludes payments/receipts. Invoice count = open lines per customer, not a separate InvNum table.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static Dictionary<string, string> LoadCustomerNameLookup()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in SageListHelpers.MapRows(Customer.List("DCLink > 0"), r => r))
        {
            var code = SageListHelpers.Col(row, "Account");
            if (string.IsNullOrWhiteSpace(code)) continue;
            var name = SageListHelpers.Col(row, "Name", "Description");
            dict[code] = string.IsNullOrWhiteSpace(name) ? code : name;
        }

        return dict;
    }

    private static string ResolveCustomerName(string code, Dictionary<string, string> names)
    {
        if (names.TryGetValue(code, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;
        try
        {
            var customer = new Customer(code);
            return customer.Description ?? code;
        }
        catch
        {
            return code;
        }
    }

    private sealed class CustomerUnpaidBucket(string code, string name)
    {
        public string Code { get; } = code;
        public string Name { get; } = name;
        public int InvoiceCount { get; set; }
        public decimal TotalOutstanding { get; set; }
    }

    private static string SupplierOpenItems(Dictionary<string, string> parameters)
    {
        var criteria = SageListHelpers.BuildCriteria(parameters, "Outstanding <> 0");
        var table = SupplierTransaction.List(criteria);
        var items = SageListHelpers.MapRows(table, row => new
        {
            autoIdx = SageListHelpers.Col(row, "AutoIdx", "ID"),
            account = SageListHelpers.Col(row, "Account", "Supplier", "cAccount", "Code"),
            reference = SageListHelpers.Col(row, "Reference"),
            description = SageListHelpers.Col(row, "Description"),
            outstanding = SageListHelpers.ParseRowAmount(row, "Outstanding", "fOutstanding", "OutstandingForeign"),
            txDate = SageListHelpers.Col(row, "TxDate", "Date")
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
        var minValuation = SageListHelpers.ParseParameterDecimal(parameters, "minValuation");
        var items = new List<InventoryListItem>();

        if (table is not null)
        {
            foreach (DataRow row in table.Rows)
            {
                var code = SageListHelpers.Col(row, "Code", "ItemCode") ?? "";
                var valuation = ParseInventoryValuation(row, code);
                var qty = SageListHelpers.Col(row, "QtyOnHand", "Quantity");

                items.Add(new InventoryListItem(
                    code,
                    SageListHelpers.Col(row, "Description") ?? "",
                    qty,
                    valuation));
            }
        }

        string? note = null;
        if (minValuation.HasValue)
        {
            var withValuation = items.Count(i => i.Valuation.HasValue);
            items = items.Where(i => i.Valuation is not null && i.Valuation >= minValuation.Value).ToList();
            note = withValuation == 0
                ? $"Valuation filter >= {minValuation.Value} requested, but Sage did not return stock valuations."
                : $"Filtered to {items.Count} item(s) with valuation >= {minValuation.Value} (read {withValuation} valuation(s) from Sage).";
        }

        var dtoItems = items.Select(i => new
        {
            code = i.Code,
            description = i.Description,
            qtyOnHand = i.QtyOnHand,
            valuation = i.Valuation
        }).ToList();

        return SageListHelpers.SerializePaged(dtoItems, criteria, parameters, note, minValuation: minValuation);
    }

    private static decimal? ParseInventoryValuation(DataRow row, string code)
    {
        var direct = SageListHelpers.ParseRowAmount(row,
            "Valuation", "StockValue", "Value", "CurValue", "ItemValue", "TotalValue");
        if (direct.HasValue) return direct;

        var qty = SageListHelpers.ParseRowAmount(row, "QtyOnHand", "Quantity");
        var unit = SageListHelpers.ParseRowAmount(row, "UnitCost", "AveUCst", "AverageCost", "Cost");
        if (qty.HasValue && unit.HasValue) return qty.Value * unit.Value;

        if (string.IsNullOrWhiteSpace(code)) return null;
        try
        {
            var item = new InventoryItem(code);
            var fromSdk = SageListHelpers.TryGetSdkPropertyDecimal(item,
                "Valuation", "Value", "StockValue", "UnitCost", "AverageCost");
            if (fromSdk.HasValue) return fromSdk;

            var sdkQty = SageListHelpers.TryGetSdkPropertyDecimal(item, "QtyOnHand", "Quantity");
            var sdkUnit = SageListHelpers.TryGetSdkPropertyDecimal(item, "UnitCost", "AverageCost");
            if (sdkQty.HasValue && sdkUnit.HasValue) return sdkQty.Value * sdkUnit.Value;
        }
        catch
        {
            return null;
        }

        return null;
    }

    private readonly record struct InventoryListItem(string Code, string Description, string? QtyOnHand, decimal? Valuation);

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
        var customerCount = TryCount(() => Customer.List("DCLink > 0"));
        var supplierCount = TryCount(() => Supplier.List("DCLink > 0"));
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
            note = "Counts from Sage lists; -1 means that query could not run (check Sage setup / company DB)."
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
