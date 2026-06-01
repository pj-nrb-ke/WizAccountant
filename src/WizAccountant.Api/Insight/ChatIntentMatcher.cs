using System.Text.RegularExpressions;

namespace WizAccountant.Api.Insight;

/// <summary>
/// Keyword/phrase routing for Insight chat. Not machine learning — add patterns here when users ask new question types.
/// </summary>
internal static class ChatIntentMatcher
{
    public const string UnpaidSalesInvoicesOp = "customer.openitems";
    public const string CustomerUnpaidSummaryOp = "customer.unpaid.summary";
    public const string CustomerAgedTopOp = "customer.aged.top";
    public const string InventoryBsNegativeLedgersOp = "inventory.bs.negative_ledgers";
    public const string SalesInvoiceDiscountCountOp = "salesinvoice.discount.count";
    public const string UnpaidSalesInvoicesCriteria = "Outstanding <> 0";

    /// <summary>Count-only: sales invoices in a year that have discounts (InvNum SQL).</summary>
    public static bool TrySalesInvoiceDiscountCount(
        string message,
        string messageLower,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string operation)
    {
        operation = SalesInvoiceDiscountCountOp;
        if (!IsSalesInvoiceDiscountCountQuery(messageLower))
            return false;

        var year = ExtractYearFromMessage(message);
        if (year.HasValue)
            parameters["year"] = year.Value.ToString();
        parameters["message"] = message;
        parameters["top"] = "1";
        tools.Add(operation);
        return true;
    }

    public static bool IsSalesInvoiceDiscountCountQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        var m = messageLower;
        if (!m.Contains("discount"))
            return false;

        var invoiceContext = m.Contains("sales invoice") || m.Contains("invoice");
        if (!invoiceContext)
            return false;

        if (!QueryAggregationMode.IsAggregationQuery(messageLower))
            return false;

        if (m.Contains("unpaid") || m.Contains("outstanding") || m.Contains("open ar"))
            return false;

        return true;
    }

    /// <summary>
    /// Inventory/stock GL accounts with credit (negative) Balance Sheet balance — PostGL on GrpTbl.StockAccLink (SAGE-BS-STOCK-NEGATIVE-001).
    /// </summary>
    public static bool TryInventoryBsNegativeLedgers(
        string messageLower,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string operation)
    {
        operation = InventoryBsNegativeLedgersOp;
        if (!IsInventoryBsNegativeLedgersQuery(messageLower))
            return false;

        tools.Add(operation);
        return true;
    }

    public static bool IsInventoryBsNegativeLedgersQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        var m = messageLower;

        // Negative stock on BS (GL credit) — not valuation-vs-GL reconciliation
        if (m.Contains("balance sheet") && (m.Contains("negative") || m.Contains("credit balance")) &&
            (m.Contains("stock") || m.Contains("inventory")) && !m.Contains("reconcil") && !m.Contains("valuation"))
            return true;

        if ((m.Contains("quantity") || m.Contains("qty") || m.Contains("on hand") || m.Contains("on-hand")) &&
            !m.Contains("balance sheet") && !m.Contains("ledger"))
            return false;

        var negativeOrCredit = m.Contains("negative") || m.Contains("credit balance") ||
                               (m.Contains("credit") && m.Contains("balance"));

        if (!negativeOrCredit)
            return false;

        if (m.Contains("balance sheet") && (m.Contains("stock") || m.Contains("inventory")))
            return true;

        if (m.Contains("negative inventory") && (m.Contains("gl") || m.Contains("ledger") || m.Contains("account")))
            return true;

        if (m.Contains("stock") && (m.Contains("ledger") || m.Contains("ledgers")))
            return negativeOrCredit;

        if (m.Contains("inventory") && m.Contains("account") && negativeOrCredit)
            return true;

        return (m.Contains("stock") || m.Contains("inventory")) &&
               (m.Contains("gl") || m.Contains("postgl") || m.Contains("general ledger")) &&
               negativeOrCredit;
    }

    public static bool TryCustomerAgedTop(
        string messageLower,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string operation)
    {
        operation = CustomerAgedTopOp;
        if (!IsCustomerAgedTopQuery(messageLower))
            return false;

        parameters["top"] = ResolveTopCount(messageLower, defaultTop: 5).ToString();
        tools.Add(operation);
        return true;
    }

    public static bool IsCustomerAgedTopQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        var m = messageLower;
        if (!m.Contains("customer") || m.Contains("supplier"))
            return false;

        var aging = m.Contains("aged") || m.Contains("aging") || m.Contains("oldest") ||
                    m.Contains("overdue") || m.Contains("age ");
        if (!aging)
            return false;

        var arBalance = m.Contains("debit") || m.Contains("balance") || m.Contains("outstanding") ||
                        m.Contains("owe") || m.Contains("receivable") || m.Contains("invoice");

        return arBalance || m.Contains("top ");
    }

    public static int ResolveTopCount(string messageLower, int defaultTop = 5)
    {
        var match = Regex.Match(messageLower, @"\btop\s*(\d+)\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var top))
            return Math.Clamp(top, 1, 50);

        match = Regex.Match(messageLower, @"\b(\d+)\s+customers?\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out top))
            return Math.Clamp(top, 1, 50);

        return messageLower.Contains("top") ? defaultTop : defaultTop;
    }

    public static bool TryCustomerUnpaidSummary(
        string messageLower,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string operation)
    {
        operation = CustomerUnpaidSummaryOp;
        if (!IsCustomerUnpaidSummaryQuery(messageLower))
            return false;

        parameters["top"] = ResolveTopCount(messageLower, defaultTop: 15).ToString();
        tools.Add(operation);
        return true;
    }

    public static bool IsCustomerUnpaidSummaryQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        if (CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(messageLower))
            return false;

        if (IsCustomerAgedTopQuery(messageLower))
            return false;

        var m = messageLower;

        if (m.Contains("top") && m.Contains("outstanding") && m.Contains("balance") &&
            !m.Contains("unpaid") && !m.Contains("invoice"))
            return false;

        if (!m.Contains("customer") || m.Contains("supplier"))
            return false;

        var unpaid = m.Contains("unpaid") || m.Contains("outstanding") || m.Contains("invoice") ||
                     m.Contains("owe") || m.Contains("receivable");
        if (!unpaid)
            return false;

        return m.Contains("highest") || m.Contains("most") || m.Contains("largest") || m.Contains("biggest") ||
               m.Contains("top customer") || m.Contains("per customer") || m.Contains("by customer") ||
               m.Contains("which customer") ||
               (m.Contains("which") && m.Contains("customer") && m.Contains("highest")) ||
               (m.Contains("highest") && m.Contains("customer") && m.Contains("balance")) ||
               (m.Contains("count") && m.Contains("invoice")) ||
               (m.Contains("list") && (m.Contains("name") || m.Contains("total")));
    }

    /// <summary>
    /// Unpaid / open AR sales invoices (Sage CustomerTransaction.List with Outstanding &lt;&gt; 0).
    /// </summary>
    public static bool TryUnpaidSalesInvoices(
        string messageLower,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string operation)
    {
        operation = UnpaidSalesInvoicesOp;
        if (!IsUnpaidSalesInvoiceQuery(messageLower))
            return false;

        parameters["criteria"] = UnpaidSalesInvoicesCriteria;
        tools.Add(operation);
        return true;
    }

    public static bool IsUnpaidSalesInvoiceQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        var m = messageLower;

        if (IsCustomerUnpaidSummaryQuery(m))
            return false;

        if (m.Contains("supplier") || m.Contains("payable") || m.Contains("purchase invoice"))
            return false;

        var invoiceContext =
            m.Contains("invoice") ||
            m.Contains("sales") ||
            m.Contains("receivable") ||
            m.Contains("accounts receivable") ||
            m.Contains(" ar ") ||
            m.StartsWith("ar ") ||
            m.EndsWith(" ar");

        if (!invoiceContext)
            return false;

        var unpaidContext =
            m.Contains("unpaid") ||
            m.Contains("not paid") ||
            m.Contains("still owing") ||
            m.Contains("owe money") ||
            m.Contains("open invoice") ||
            m.Contains("open ar");

        if (unpaidContext)
            return true;

        if (QueryAggregationMode.IsAggregationQuery(m) && invoiceContext)
            return false;

        var fetchList =
            m.Contains("get me") || m.Contains("get ") || m.Contains("give me") ||
            m.Contains("show me") || m.Contains("fetch") || m.Contains("pull") ||
            m.Contains("list ") || m.StartsWith("list ") || m.Contains("return ");

        if (fetchList && (m.Contains("unpaid") || m.Contains("outstanding") || m.Contains("open ")))
            return true;

        if (m.Contains("sales invoice") && (m.Contains("unpaid") || m.Contains("outstanding") || m.Contains("open ")))
            return true;

        if (m.Contains("outstanding") && invoiceContext)
            return true;

        return false;
    }

    public static int? ExtractYearFromMessage(string message)
    {
        var match = Regex.Match(message, @"\b(20\d{2})\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var y) ? y : null;
    }

    public static bool WantsRowPreview(string? message) =>
        !QueryAggregationMode.IsAggregationQuery(message);
}
