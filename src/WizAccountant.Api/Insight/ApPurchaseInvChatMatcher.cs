using System.Text.RegularExpressions;



namespace WizAccountant.Api.Insight;



/// <summary>AP + Purchase analytical routing (SAGE-TRAIN-004).</summary>

internal static class ApPurchaseInvChatMatcher

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



        if (!IsApOrPurchaseContext(m))

            return false;



        if (TrySupplierUnpaidSummary(m, parameters, tools, out operation))

            return true;

        if (TrySupplierAgedTop(m, parameters, tools, out operation))

            return true;

        if (TryOverdueCount(m, parameters, tools, out operation))

            return true;

        if (TryPurchaseDiscountCount(message, m, parameters, tools, out operation))

            return true;

        if (TryPurchaseCount(m, parameters, tools, out operation))

            return true;

        if (TryDuplicate(m, parameters, tools, out operation))

            return true;

        if (TrySupplierCreditNote(m, parameters, tools, out operation))
            return true;

        if (TryCreditBalances(m, parameters, tools, out operation))

            return true;

        if (TryPartiallyPaid(m, parameters, tools, out operation))

            return true;

        if (TryUnpaidOlderThan(m, parameters, tools, out operation))

            return true;

        if (TryPaymentsTop(m, parameters, tools, out operation))

            return true;

        if (TryPurchaseDiscountTop(m, parameters, tools, out operation))

            return true;

        if (TryPurchaseTop(m, parameters, tools, out operation))

            return true;

        if (TryOutstandingTop(m, parameters, tools, out operation))

            return true;

        if (TryPurchasesTop(m, parameters, tools, out operation))

            return true;



        return false;

    }



    private static bool IsApOrPurchaseContext(string m) =>

        m.Contains("supplier") || m.Contains("creditor") || m.Contains("payable") ||

        m.Contains("purchase") || m.Contains(" ap ") || m.StartsWith("ap ") ||

        m.Contains("vendor");



    private static void ApplyCommonParameters(string message, string m, Dictionary<string, string> parameters)

    {

        var top = ChatIntentMatcher.ResolveTopCount(m, 0);

        if (top > 0)

            parameters["top"] = top.ToString();

        var year = ChatIntentMatcher.ExtractYearFromMessage(message);

        if (year.HasValue)

            parameters["year"] = year.Value.ToString();

        var days = ExtractMinDays(m);

        if (days.HasValue)

            parameters["minDaysOutstanding"] = days.Value.ToString();

        if (m.Contains("month") && m.Contains("this"))

            parameters["month"] = DateTime.Today.Month.ToString();

        parameters["message"] = message;

    }



    private static bool TrySupplierUnpaidSummary(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!ChatIntentMatcher.TrySupplierUnpaidRoute(m, parameters, tools, out var operation))
            return false;
        op = operation;
        return true;
    }

    private static bool TrySupplierAgedTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "supplier.aged.top";

        if (!m.Contains("supplier") && !m.Contains("creditor"))

            return false;

        if (!m.Contains("oldest") && !m.Contains("aged"))

            return false;

        if (m.Contains("customer"))

            return false;

        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 5).ToString();

        tools.Add(op);

        return true;

    }



    private static bool TryOverdueCount(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "ap.invoice.overdue.count";

        if (!QueryAggregationMode.IsAggregationQuery(m))

            return false;

        if (!m.Contains("overdue") && !m.Contains("unpaid past"))

            return false;

        if (!m.Contains("supplier") && !m.Contains("purchase") && !m.Contains("ap"))

            return false;

        if (!m.Contains("invoice"))

            return false;

        tools.Add(op);

        return true;

    }



    private static bool TryPurchaseDiscountCount(string message, string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "purchaseinvoice.discount.count";

        if (!m.Contains("discount"))

            return false;

        if (!m.Contains("purchase") && !m.Contains("supplier"))

            return false;

        if (!m.Contains("invoice"))

            return false;

        if (!QueryAggregationMode.IsAggregationQuery(m))

            return false;

        if (m.Contains("sales"))

            return false;

        parameters["message"] = message;

        tools.Add(op);

        return true;

    }



    private static bool TryPurchaseCount(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "purchaseinvoice.count";

        if (!QueryAggregationMode.IsAggregationQuery(m))

            return false;

        if (!m.Contains("purchase") && !m.Contains("supplier"))

            return false;

        if (!m.Contains("invoice"))

            return false;

        if (m.Contains("discount") || m.Contains("overdue"))

            return false;

        tools.Add(op);

        return true;

    }



    private static bool TryDuplicate(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "purchaseinvoice.duplicate";

        if (!m.Contains("duplicate"))

            return false;

        if (m.Contains("journal") || m.Contains("posting") || m.Contains("gl "))

            return false;

        if (!m.Contains("supplier") && !m.Contains("purchase"))

            return false;

        if (!m.Contains("invoice"))

            return false;

        tools.Add(op);

        return true;

    }



    private static bool TrySupplierCreditNote(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = SupplierCreditNoteChatHelper.ResolveOperation(m);
        if (!SupplierCreditNoteChatHelper.IsSupplierCreditNoteQuery(m))
            return false;

        if (op == SupplierCreditNoteChatHelper.CountOperation)
            parameters["top"] = "1";
        tools.Add(op);
        return true;
    }



    private static bool TryCreditBalances(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "supplier.credit.balances";

        if (SupplierCreditNoteChatHelper.IsSupplierCreditNoteQuery(m))
            return false;

        if (!m.Contains("supplier") && !m.Contains("creditor"))

            return false;

        if (!m.Contains("credit") && !m.Contains("overpaid"))

            return false;

        if (m.Contains("customer"))

            return false;

        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 25).ToString();

        tools.Add(op);

        return true;

    }



    private static bool TryPartiallyPaid(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "purchaseinvoice.partially.paid";

        if (!m.Contains("partial") && !m.Contains("part paid") && !m.Contains("partly paid"))

            return false;

        if (!m.Contains("supplier") && !m.Contains("purchase"))

            return false;

        tools.Add(op);

        return true;

    }



    private static bool TryUnpaidOlderThan(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "supplier.invoice.unpaid.olderthan";

        if (!m.Contains("supplier") && !m.Contains("ap") && !m.Contains("purchase"))

            return false;

        if (!m.Contains("invoice"))

            return false;

        var hasAge = m.Contains("older") || m.Contains("days") || ExtractMinDays(m).HasValue;

        if (!hasAge)

            return false;

        if (!m.Contains("unpaid") && !m.Contains("outstanding") && !m.Contains("invoice"))

            return false;

        // invoice + age is sufficient (e.g. "supplier invoices older than 90 days")

        if (QueryAggregationMode.IsAggregationQuery(m))

            return false;

        parameters["minDaysOutstanding"] = (ExtractMinDays(m) ?? 90).ToString();

        tools.Add(op);

        return true;

    }



    private static bool TryPaymentsTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "supplier.payments.top";

        if (!m.Contains("payment") && !m.Contains("paid"))

            return false;

        if (!m.Contains("supplier"))

            return false;

        if (!m.Contains("top") && !m.Contains("highest") && !m.Contains("most"))

            return false;

        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();

        tools.Add(op);

        return true;

    }



    private static bool TryPurchaseDiscountTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "purchaseinvoice.discount.top";

        if (!m.Contains("discount"))

            return false;

        if (!m.Contains("purchase") && !m.Contains("supplier"))

            return false;

        if (QueryAggregationMode.IsAggregationQuery(m))

            return false;

        if (!m.Contains("top") && !m.Contains("highest"))

            return false;

        if (m.Contains("sales"))

            return false;

        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 5).ToString();

        tools.Add(op);

        return true;

    }



    private static bool TryPurchaseTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "purchaseinvoice.top";

        if (!m.Contains("purchase") && !m.Contains("supplier"))

            return false;

        if (!m.Contains("invoice"))

            return false;

        if (m.Contains("discount") || m.Contains("duplicate"))

            return false;

        if (!m.Contains("top") && !m.Contains("highest"))

            return false;

        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();

        tools.Add(op);

        return true;

    }



    private static bool TryPurchasesTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "supplier.purchases.top";

        if (!m.Contains("purchase") && !m.Contains("purchases") && !m.Contains("supplied"))

            return false;

        if (m.Contains("outstanding") || m.Contains("owe"))

            return false;

        if (!m.Contains("top") && !m.Contains("highest"))

            return false;

        if (m.Contains("payment"))

            return false;

        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();

        tools.Add(op);

        return true;

    }



    private static bool TryOutstandingTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)

    {

        op = "supplier.outstanding.top";

        if (!m.Contains("supplier") && !m.Contains("creditor"))

            return false;

        if (!m.Contains("outstanding") && !m.Contains("owe"))

            return false;

        if (!m.Contains("top") && !m.Contains("most") && !m.Contains("highest"))

            return false;

        if (m.Contains("oldest") || m.Contains("aged"))

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

        if (m.Contains("365") || m.Contains("12 month")) return 365;

        if (m.Contains("120")) return 120;

        if (m.Contains("90")) return 90;

        return null;

    }

}


