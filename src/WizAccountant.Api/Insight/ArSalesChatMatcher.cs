using System.Text.RegularExpressions;

namespace WizAccountant.Api.Insight;

/// <summary>AR + Sales analytical handler routing (SAGE-TRAIN-003).</summary>
internal static class ArSalesChatMatcher
{
    public static bool TryRoute(
        string message,
        string m,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;
        ApplyCommonParameters(message, m, parameters);

        if (TryCreditNoteCount(message, m, parameters, tools, out operation))
            return true;
        if (TryDebitNote(message, m, parameters, tools, out operation))
            return true;
        if (TryDiscountCount(message, m, parameters, tools, out operation))
            return true;
        if (TryDiscountTop(m, parameters, tools, out operation))
            return true;
        if (TryOverdueBuckets(m, parameters, tools, out operation))
            return true;
        if (TryCreditLimit(m, parameters, tools, out operation))
            return true;
        if (TryPartiallyPaid(m, parameters, tools, out operation))
            return true;
        if (TryUnpaidOlderThan(m, parameters, tools, out operation))
            return true;
        if (TryAgedCreditTop(m, parameters, tools, out operation))
            return true;
        if (TryOutstandingDebitTop(m, parameters, tools, out operation))
            return true;
        if (TrySalesTop(message, m, parameters, tools, out operation))
            return true;

        return false;
    }

    private static void ApplyCommonParameters(string message, string m, Dictionary<string, string> parameters)
    {
        var top = ChatIntentMatcher.ResolveTopCount(m, defaultTop: 0);
        if (top > 0)
            parameters["top"] = top.ToString();

        var year = ChatIntentMatcher.ExtractYearFromMessage(message);
        if (year.HasValue)
            parameters["year"] = year.Value.ToString();

        var minDays = ExtractMinDays(m);
        if (minDays.HasValue)
            parameters["minDaysOutstanding"] = minDays.Value.ToString();

        parameters["message"] = message;
    }

    private static bool TryDebitNote(string message, string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = DebitNoteChatHelper.ResolveOperation(m);
        if (!DebitNoteChatHelper.IsSalesDebitNoteQuery(m))
            return false;

        parameters["message"] = message;
        if (op == DebitNoteChatHelper.CountOperation)
            parameters["top"] = "1";
        tools.Add(op);
        return true;
    }

    private static bool TryCreditNoteCount(string message, string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = CreditNoteChatHelper.SalesCreditNoteCountOperation;
        if (!CreditNoteChatHelper.IsSalesCreditNoteCountQuery(m))
            return false;

        parameters["message"] = message;
        parameters["top"] = "1";
        tools.Add(op);
        return true;
    }

    private static bool TryDiscountCount(string message, string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        if (ChatIntentMatcher.TrySalesInvoiceDiscountCount(message, m, parameters, tools, out var operation))
        {
            op = operation;
            return true;
        }

        op = null;
        return false;
    }

    private static bool TryDiscountTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "salesinvoice.discount.top";
        if (!m.Contains("discount") || !m.Contains("invoice"))
            return false;
        if (QueryAggregationMode.IsAggregationQuery(m))
            return false;
        if (!m.Contains("top") && !m.Contains("highest") && !m.Contains("largest"))
            return false;
        if (m.Contains("customer"))
            return false;
        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 5).ToString();
        tools.Add(op);
        return true;
    }

    private static bool TryOverdueBuckets(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "ar.invoice.overdue.buckets";
        if (!m.Contains("overdue") && !m.Contains("aging bucket"))
            return false;
        if (!m.Contains("count") && !m.Contains("how many") && !m.Contains("number of"))
            return false;
        if (!m.Contains("invoice") && !m.Contains("customer"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryCreditLimit(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "customer.over.creditlimit";
        if (!m.Contains("credit limit"))
            return false;
        if (!m.Contains("exceed") && !m.Contains("over") && !m.Contains("above") && !m.Contains("beyond"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryPartiallyPaid(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "salesinvoice.partially.paid";
        if (!m.Contains("partial") && !m.Contains("part paid") && !m.Contains("partly paid"))
            return false;
        if (!m.Contains("invoice"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryUnpaidOlderThan(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "customer.invoice.unpaid.olderthan";
        if (!m.Contains("unpaid") && !m.Contains("outstanding"))
            return false;
        if (!m.Contains("invoice") && !m.Contains("older") && !m.Contains("days"))
            return false;
        if (m.Contains("how many") || m.Contains("count "))
            return false;
        var days = ExtractMinDays(m) ?? 30;
        parameters["minDaysOutstanding"] = days.ToString();
        tools.Add(op);
        return true;
    }

    private static bool TryAgedCreditTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "customer.aged.credit.top";
        if (!m.Contains("credit") || !m.Contains("customer"))
            return false;
        if (!m.Contains("oldest") && !m.Contains("aged"))
            return false;
        if (m.Contains("debit"))
            return false;
        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 5).ToString();
        tools.Add(op);
        return true;
    }

    private static bool TryOutstandingDebitTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "customer.outstanding.debit.top";
        if (ChatIntentMatcher.IsCustomerAgedTopQuery(m) || ChatIntentMatcher.IsCustomerUnpaidSummaryQuery(m))
            return false;
        if (CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(m))
            return false;
        if (!m.Contains("customer"))
            return false;
        if (!m.Contains("outstanding") && !m.Contains("owe") && !m.Contains("balance"))
            return false;
        if (!m.Contains("top") && !m.Contains("highest") && !m.Contains("most"))
            return false;
        if (m.Contains("oldest") || m.Contains("aged"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TrySalesTop(string message, string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "customer.sales.top";
        if (!m.Contains("customer") && !m.Contains("sales"))
            return false;
        if (!m.Contains("sales") && !m.Contains("revenue") && !m.Contains("turnover"))
            return false;
        if (!m.Contains("top") && !m.Contains("highest") && !m.Contains("most"))
            return false;
        if (m.Contains("unpaid") || m.Contains("outstanding") || m.Contains("discount"))
            return false;
        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();
        tools.Add(op);
        return true;
    }

    private static int? ExtractMinDays(string m)
    {
        var match = Regex.Match(m, @"\b(\d+)\s*days?\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var d))
            return d;
        if (m.Contains("90")) return 90;
        if (m.Contains("60")) return 60;
        if (m.Contains("30")) return 30;
        return null;
    }
}
