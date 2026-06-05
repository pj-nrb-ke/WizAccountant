using System.Text.Json;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

internal static class ChatResultGridBuilder
{
    public static ChatGridDto? TryBuild(string operation, string? resultJson) =>
        TryBuild(operation, resultJson, null, RankingQueryPolicy.MaxGridRows);

    public static ChatGridDto? TryBuild(
        string operation,
        string? resultJson,
        SageIntentEngine.Classification? classification,
        int maxRows)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return null;

        if (classification?.PrimaryIntent == SageIntentEngine.IntentType.Aggregation)
            return null;

        var cap = maxRows;
        if (classification is not null && RankingQueryPolicy.ShouldCapGrid(operation, classification, null))
            cap = Math.Min(cap, RankingQueryPolicy.MaxGridRows);

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            return operation switch
            {
                "customer.unpaid.summary" => FromTopCustomers(root, cap),
                "customer.aged.top" => FromAgedCustomers(root, cap),
                "customer.aged.credit.top" => FromAgedCustomers(root, cap),
                "customer.outstanding.debit.top" => FromTopCustomers(root, cap),
                "customer.sales.top" => FromTopCustomersSales(root, cap),
                "salesinvoice.discount.top" => FromTopInvoices(root, cap),
                "salesinvoice.partially.paid" => FromArInvoices(root, cap),
                "customer.invoice.unpaid.olderthan" => FromArInvoices(root, cap),
                "customer.over.creditlimit" => FromCreditCustomers(root, cap),
                "customer.credit.balances" => FromCreditCustomers(root, cap),
                "supplier.aged.top" => FromAgedSuppliers(root, cap),
                "inventory.gl.reconcile" => FromInventoryReconcile(root),
                "inventory.bs.negative_ledgers" => FromNegativeStockLedgers(root),
                "product.monthly.orders.analysis" => FromProductMonthlyBreakdown(root, cap),
                PurchaseProductQuarterlyChatMatcher.Operation => FromPurchaseProductQuarterly(root, cap),
                CustomerCollectionsHelper.ByMonthOperation => FromCollectionsMonthly(root),
                CustomerCollectionsHelper.ByCustomerOperation or CustomerCollectionsHelper.TopOperation => FromCollectionsByCustomer(root, cap),
                CustomerCollectionsHelper.SummaryOperation => FromCollectionsMonthly(root) ?? FromCollectionsSummary(root),
                "search.global" => FromHits(root, cap),
                _ => FromItemsArray(root, cap) ?? FromKpis(root)
            };
        }
        catch
        {
            return null;
        }
    }

    private static ChatGridDto? FromCreditCustomers(JsonElement root, int maxRows = 50)
    {
        if (!root.TryGetProperty("customers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = arr.EnumerateArray().Take(maxRows).Select(r => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["#"] = Prop(r, "rank"),
            ["Customer code"] = Prop(r, "code"),
            ["Customer name"] = Prop(r, "name"),
            ["Balance"] = FormatMoney(r, "balance")
        }).ToList();

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static ChatGridDto? FromAgedSuppliers(JsonElement root, int maxRows = 50)
    {
        if (!root.TryGetProperty("topSuppliers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = arr.EnumerateArray().Take(maxRows).Select(r => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["#"] = Prop(r, "rank"),
            ["Supplier code"] = Prop(r, "code"),
            ["Supplier name"] = Prop(r, "name"),
            ["Outstanding"] = FormatMoney(r, "totalOutstanding"),
            ["Oldest invoice"] = Prop(r, "oldestInvoiceDate"),
            ["Days outstanding"] = Prop(r, "daysOutstanding")
        }).ToList();

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static ChatGridDto? FromAgedCustomers(JsonElement root, int maxRows = 50)
    {
        if (!root.TryGetProperty("topCustomers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = new List<Dictionary<string, string?>>();
        foreach (var r in arr.EnumerateArray().Take(maxRows))
        {
            rows.Add(new(StringComparer.OrdinalIgnoreCase)
            {
                ["#"] = Prop(r, "rank"),
                ["Customer code"] = Prop(r, "code"),
                ["Customer name"] = Prop(r, "name"),
                ["Balance"] = FormatMoney(r, "totalOutstanding"),
                ["Oldest invoice"] = Prop(r, "oldestInvoiceDate"),
                ["Days outstanding"] = Prop(r, "daysOutstanding")
            });
        }

        return rows.Count == 0 ? null : new ChatGridDto
        {
            Columns = rows[0].Keys.ToList(),
            Rows = rows
        };
    }

    private static ChatGridDto? FromTopCustomers(JsonElement root, int maxRows = 50)
    {
        if (!root.TryGetProperty("topCustomers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = new List<Dictionary<string, string?>>();
        foreach (var r in arr.EnumerateArray().Take(maxRows))
        {
            rows.Add(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customer code"] = Prop(r, "code"),
                ["Customer name"] = Prop(r, "name"),
                ["Open lines"] = Prop(r, "invoiceCount"),
                ["Total outstanding"] = FormatMoney(r, "totalOutstanding")
            });
        }

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static ChatGridDto? FromNegativeStockLedgers(JsonElement root)
    {
        if (!root.TryGetProperty("ledgers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = new List<Dictionary<string, string?>>();
        foreach (var l in arr.EnumerateArray().Take(100))
        {
            rows.Add(new(StringComparer.OrdinalIgnoreCase)
            {
                ["GL account"] = Prop(l, "glAccount"),
                ["GL name"] = Prop(l, "glAccountName"),
                ["Balance"] = FormatMoneyProp(l, "netBalance")
            });
        }

        if (rows.Count == 0)
        {
            rows.Add(new(StringComparer.OrdinalIgnoreCase)
            {
                ["GL account"] = "(none)",
                ["GL name"] = "No credit balances on inventory stock GL accounts",
                ["Balance"] = "0.00"
            });
        }
        else if (root.TryGetProperty("totalNegativeStockValue", out var total) && total.ValueKind == JsonValueKind.Number)
        {
            rows.Add(new(StringComparer.OrdinalIgnoreCase)
            {
                ["GL account"] = "★ TOTAL",
                ["GL name"] = "Total negative stock value",
                ["Balance"] = total.GetDecimal().ToString("N2")
            });
        }

        return new ChatGridDto
        {
            Columns = new List<string> { "GL account", "GL name", "Balance" },
            Rows = rows
        };
    }

    private static ChatGridDto? FromInventoryReconcile(JsonElement root)
    {
        var validation = InventoryReconcileValidator.Validate(root);
        var gl = GetJsonDecimal(root, "balanceSheetStockValue", "balanceSheetInventoryGl");
        var val = GetJsonDecimal(root, "inventoryValuation");
        var diff = GetJsonDecimal(root, "difference");
        var match = validation.Passed && root.TryGetProperty("matches", out var m) && m.GetBoolean() ? "Yes" : "No";

        var rows = new List<Dictionary<string, string?>>
        {
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Row"] = validation.Passed ? "★ GRAND TOTAL" : "⚠ SANITY FAILED",
                ["GL account"] = validation.Passed
                    ? "(all distinct inventory stock GL accounts)"
                    : (validation.FailureReason ?? "Incomplete valuation"),
                ["GL name"] = root.TryGetProperty("valuationLineCount", out var vlc) && vlc.ValueKind == JsonValueKind.Number
                    ? $"SQL lines: {vlc.GetInt32()}"
                    : "",
                ["Balance Sheet (GL)"] = gl.ToString("N2"),
                ["Inventory Valuation"] = val.ToString("N2"),
                ["Difference"] = diff.ToString("N2"),
                ["Match"] = match
            }
        };

        if (root.TryGetProperty("accounts", out var accounts) && accounts.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in accounts.EnumerateArray().Take(50))
            {
                rows.Add(new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Row"] = "Detail",
                    ["GL account"] = Prop(a, "glAccount"),
                    ["GL name"] = Prop(a, "glAccountName"),
                    ["Balance Sheet (GL)"] = FormatMoneyProp(a, "balanceSheet"),
                    ["Inventory Valuation"] = FormatMoneyProp(a, "inventoryValuation"),
                    ["Difference"] = FormatMoneyProp(a, "difference"),
                    ["Match"] = Prop(a, "match")
                });
            }
        }

        if (root.TryGetProperty("stockGroupValuation", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in groups.EnumerateArray().Take(15))
            {
                rows.Add(new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Row"] = "Stock group",
                    ["GL account"] = Prop(g, "glAccount"),
                    ["GL name"] = Prop(g, "stockGroupName"),
                    ["Balance Sheet (GL)"] = "",
                    ["Inventory Valuation"] = FormatMoneyProp(g, "inventoryValuation"),
                    ["Difference"] = "",
                    ["Match"] = Prop(g, "stockGroup")
                });
            }
        }

        var columns = new List<string> { "Row", "GL account", "GL name", "Balance Sheet (GL)", "Inventory Valuation", "Difference", "Match" };
        return new ChatGridDto { Columns = columns, Rows = rows };
    }

    private static decimal GetJsonDecimal(JsonElement root, string primary, string? alt = null)
    {
        if (root.TryGetProperty(primary, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDecimal();
        if (alt is not null && root.TryGetProperty(alt, out el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDecimal();
        return 0;
    }

    private static string FormatMoneyProp(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDecimal().ToString("N2")
            : Prop(el, name) ?? "";

    private static ChatGridDto? FromStockGroups(JsonElement root)
    {
        if (!root.TryGetProperty("stockGroups", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = new List<Dictionary<string, string?>>();
        foreach (var g in arr.EnumerateArray())
        {
            rows.Add(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Stock group"] = Prop(g, "stockGroup"),
                ["Group name"] = Prop(g, "stockGroupName"),
                ["GL account"] = Prop(g, "glAccountCode"),
                ["GL name"] = Prop(g, "glAccountName"),
                ["GL net balance"] = FormatMoney(g, "glNetBalance")
            });
        }

        if (rows.Count > 0)
            return new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };

        return SummaryRow("Balance Sheet inventory (GL)", FormatMoney(root, "balanceSheetInventoryGl"),
            "Valuation (SDK sum)", FormatMoney(root, "inventoryValuationProxy"),
            "Difference", FormatMoney(root, "difference"),
            "Matches", root.TryGetProperty("matches", out var m) ? (m.GetBoolean() ? "Yes" : "No") : "");
    }

    private static ChatGridDto SummaryRow(params string[] pairs)
    {
        var row = new Dictionary<string, string?>();
        for (var i = 0; i < pairs.Length - 1; i += 2)
            row[pairs[i]] = pairs[i + 1];
        return new ChatGridDto
        {
            Columns = new List<string> { "Measure", "Value" },
            Rows = row.Select(kv => new Dictionary<string, string?> { ["Measure"] = kv.Key, ["Value"] = kv.Value }).ToList()
        };
    }

    private static ChatGridDto? FromProductMonthlyBreakdown(JsonElement root, int maxRows)
    {
        if (!root.TryGetProperty("monthlyBreakdown", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = arr.EnumerateArray().Take(maxRows).Select(r => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Year"] = Prop(r, "salesYear"),
            ["Month"] = Prop(r, "salesMonthName").Length > 0 ? Prop(r, "salesMonthName") : Prop(r, "month"),
            ["Product code"] = Prop(r, "productCode"),
            ["Product name"] = Prop(r, "productName"),
            ["Quantity sold"] = Prop(r, "quantity"),
            ["Value"] = FormatMoney(r, "value")
        }).ToList();

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static ChatGridDto? FromPurchaseProductQuarterly(JsonElement root, int maxRows)
    {
        if (!root.TryGetProperty("periodBreakdown", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            if (!root.TryGetProperty("quarterlyBreakdown", out arr) || arr.ValueKind != JsonValueKind.Array)
                return null;
        }

        var periodLabel = root.TryGetProperty("groupBy", out var gb) &&
                          string.Equals(gb.GetString(), "month", StringComparison.OrdinalIgnoreCase)
            ? "Month"
            : "Quarter";

        var rows = arr.EnumerateArray().Take(maxRows).Select(r => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [periodLabel] = string.IsNullOrEmpty(Prop(r, "periodName")) ? Prop(r, "quarterName") : Prop(r, "periodName"),
            ["Quantity"] = FormatMoney(r, "totalQuantity"),
            ["Value"] = FormatMoney(r, "totalValue")
        }).ToList();

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static ChatGridDto? FromHits(JsonElement root, int maxRows = 50)
    {
        if (!root.TryGetProperty("hits", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        return FromObjectArray(arr, maxRows, "type", "code", "name");
    }

    private static ChatGridDto? FromItemsArray(JsonElement root, int maxRows = 50)
    {
        if (!root.TryGetProperty("items", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var list = arr.EnumerateArray().Take(maxRows).ToList();
        if (list.Count == 0) return null;

        var colSet = new List<string>();
        foreach (var prop in list[0].EnumerateObject())
            colSet.Add(HumanizeColumn(prop.Name));

        var rows = new List<Dictionary<string, string?>>();
        foreach (var item in list)
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in item.EnumerateObject())
                row[HumanizeColumn(prop.Name)] = ElementToString(prop.Value);
            rows.Add(row);
        }

        return new ChatGridDto { Columns = colSet, Rows = rows };
    }

    private static ChatGridDto? FromKpis(JsonElement root)
    {
        if (!root.TryGetProperty("kpis", out var kpis))
            return null;

        var rows = new List<Dictionary<string, string?>>();
        foreach (var prop in kpis.EnumerateObject())
        {
            rows.Add(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["KPI"] = HumanizeColumn(prop.Name),
                ["Value"] = ElementToString(prop.Value)
            });
        }

        return rows.Count == 0 ? null : new ChatGridDto
        {
            Columns = new List<string> { "KPI", "Value" },
            Rows = rows
        };
    }

    private static ChatGridDto? FromObjectArray(JsonElement arr, int maxRows, params string[] props)
    {
        var rows = new List<Dictionary<string, string?>>();
        foreach (var r in arr.EnumerateArray().Take(maxRows))
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in props)
                row[HumanizeColumn(p)] = Prop(r, p);
            rows.Add(row);
        }

        return rows.Count == 0 ? null : new ChatGridDto
        {
            Columns = props.Select(HumanizeColumn).ToList(),
            Rows = rows
        };
    }

    private static ChatGridDto? FromTopInvoices(JsonElement root, int maxRows)
    {
        if (!root.TryGetProperty("topInvoices", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = arr.EnumerateArray().Take(maxRows).Select(r => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["#"] = Prop(r, "rank"),
            ["Invoice"] = Prop(r, "invoiceNumber"),
            ["Date"] = Prop(r, "invoiceDate"),
            ["Discount"] = FormatMoney(r, "discountValue"),
            ["Total"] = FormatMoney(r, "invoiceTotal")
        }).ToList();

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static ChatGridDto? FromArInvoices(JsonElement root, int maxRows)
    {
        if (!root.TryGetProperty("invoices", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = arr.EnumerateArray().Take(maxRows).Select(r => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["#"] = Prop(r, "rank"),
            ["Customer"] = Prop(r, "customerCode"),
            ["Reference"] = Prop(r, "reference"),
            ["Outstanding"] = FormatMoney(r, "outstanding"),
            ["Days"] = Prop(r, "daysOutstanding")
        }).ToList();

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static ChatGridDto? FromTopCustomersSales(JsonElement root, int maxRows)
    {
        if (!root.TryGetProperty("topCustomers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = arr.EnumerateArray().Take(maxRows).Select(r => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["#"] = Prop(r, "rank"),
            ["Customer code"] = Prop(r, "code"),
            ["Customer name"] = Prop(r, "name"),
            ["Sales value"] = FormatMoney(r, "salesValue"),
            ["Invoices"] = Prop(r, "invoiceCount")
        }).ToList();

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static string? Prop(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) ? ElementToString(v) : null;

    private static string? FormatMoney(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number)
            return Prop(el, name);
        return v.GetDecimal().ToString("N2");
    }

    private static string? ElementToString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Number => v.GetDecimal().ToString("N2"),
        JsonValueKind.True => "Yes",
        JsonValueKind.False => "No",
        JsonValueKind.Null => "",
        _ => v.ToString()
    };

    private static ChatGridDto? FromCollectionsMonthly(JsonElement root)
    {
        if (!root.TryGetProperty("monthlyBreakdown", out var arr) || arr.ValueKind != JsonValueKind.Array ||
            arr.GetArrayLength() == 0)
            return null;

        var rows = arr.EnumerateArray().Select(r =>
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Year"] = Prop(r, "year"),
                ["Month"] = Prop(r, "month"),
                ["Collection amount"] = FormatMoney(r, "collectionAmount")
            };
            var seg = Prop(r, "segmentLabel");
            if (!string.IsNullOrWhiteSpace(seg))
                row["Period segment"] = seg;
            return row;
        }).ToList();

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static ChatGridDto? FromCollectionsSummary(JsonElement root)
    {
        if (!root.TryGetProperty("totalCollections", out var total) || total.ValueKind != JsonValueKind.Number)
            return null;

        var rows = new List<Dictionary<string, string?>>
        {
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Period from"] = root.TryGetProperty("dateFrom", out var df) ? df.GetString() : "",
                ["Period to"] = root.TryGetProperty("dateTo", out var dt) ? dt.GetString() : "",
                ["Total collections"] = total.GetDecimal().ToString("N2")
            }
        };

        return new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static ChatGridDto? FromCollectionsByCustomer(JsonElement root, int maxRows)
    {
        if (!root.TryGetProperty("byCustomer", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var rows = arr.EnumerateArray().Take(maxRows).Select(r => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["#"] = Prop(r, "rank"),
            ["Customer code"] = Prop(r, "customerCode"),
            ["Customer name"] = Prop(r, "customerName"),
            ["Collection amount"] = FormatMoney(r, "collectionAmount")
        }).ToList();

        return rows.Count == 0 ? null : new ChatGridDto { Columns = rows[0].Keys.ToList(), Rows = rows };
    }

    private static string HumanizeColumn(string name) =>
        name switch
        {
            "code" => "Code",
            "name" => "Name",
            "description" => "Description",
            "balance" => "Balance",
            "valuation" => "Valuation",
            "account" => "Account",
            "reference" => "Reference",
            "outstanding" => "Outstanding",
            "txDate" => "Date",
            "invoiceCount" => "Open lines",
            "totalOutstanding" => "Total outstanding",
            _ => char.ToUpper(name[0]) + name[1..]
        };
}
