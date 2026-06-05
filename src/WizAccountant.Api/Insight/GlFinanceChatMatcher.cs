namespace WizAccountant.Api.Insight;

/// <summary>GL + VAT + Treasury + Bank routing (SAGE-TRAIN-005).</summary>
internal static class GlFinanceChatMatcher
{
    public static bool TryRoute(
        string message,
        string m,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;
        ApplyCommon(message, m, parameters);

        if (TryTreasury(m, parameters, tools, out operation)) return true;
        if (TryVat(message, m, parameters, tools, out operation)) return true;
        if (TryGlBalanceUnusual(m, parameters, tools, out operation)) return true;
        if (TryBank(m, parameters, tools, out operation)) return true;
        if (TryGlAudit(m, parameters, tools, out operation)) return true;
        if (TryGlAnalytics(m, parameters, tools, out operation)) return true;
        if (TryInventoryGl(m, parameters, tools, out operation)) return true;

        return false;
    }

    private static void ApplyCommon(string message, string m, Dictionary<string, string> parameters)
    {
        var top = ChatIntentMatcher.ResolveTopCount(m, 0);
        if (top > 0) parameters["top"] = top.ToString();
        var year = ChatIntentMatcher.ExtractYearFromMessage(message);
        if (year.HasValue) parameters["year"] = year.Value.ToString();
        if (m.Contains("30 day") || m.Contains("30-day")) parameters["horizonDays"] = "30";
        parameters["message"] = message;
    }

    private static bool IsGlContext(string m) =>
        m.Contains("gl ") || m.Contains("general ledger") || m.Contains("postgl") ||
        m.Contains("journal") || m.Contains("trial balance") || m.Contains("ledger");

    private static bool TryTreasury(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (CustomerCollectionsHelper.IsCustomerCollectionsQuery(m))
            return false;

        if ((m.Contains("cash") || m.Contains("liquidity")) &&
            (m.Contains("low") || m.Contains("shortage") ||
             (m.Contains("why") && !m.Contains("customer") && !m.Contains("supplier"))) &&
            !m.Contains("net cash") && !m.Contains("netcash") && !m.Contains("forecast"))
        {
            op = "treasury.dashboard";
            tools.Add(op);
            return true;
        }

        if (!m.Contains("treasury") && !m.Contains("cash forecast") && !m.Contains("forecast") &&
            !m.Contains("cashflow") && !m.Contains("cash flow") && !m.Contains("cash position") &&
            !m.Contains("liquidity") && !m.Contains("cash burn") &&
            !m.Contains("collections next") && !m.Contains("payments next") &&
            !CustomerCollectionsHelper.IsCollectionsForecastQuery(m))
            return false;

        if (m.Contains("dashboard") || m.Contains("summary") || m.Contains("treasury summary"))
        {
            op = "treasury.dashboard";
            tools.Add(op);
            return true;
        }

        if (m.Contains("expected collection") || m.Contains("collections next") || m.Contains("expected customer") ||
            (m.Contains("receivable") && m.Contains("forecast")))
        {
            op = "treasury.collections.forecast";
            tools.Add(op);
            return true;
        }

        if (m.Contains("supplier payment") || (m.Contains("payment") && m.Contains("supplier")))
        {
            op = "treasury.payments.forecast";
            tools.Add(op);
            return true;
        }

        if (m.Contains("net cash") || m.Contains("netcash"))
        {
            op = "treasury.netcashflow.forecast";
            tools.Add(op);
            return true;
        }

        if (m.Contains("forecast") || m.Contains("cash position") || m.Contains("cash shortage"))
        {
            op = "treasury.cash.forecast";
            tools.Add(op);
            return true;
        }

        op = "treasury.dashboard";
        tools.Add(op);
        return true;
    }

    private static bool TryGlBalanceUnusual(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        var unusual = m.Contains("unusual") || m.Contains("abnormal");
        if (unusual && m.Contains("balance") && (m.Contains("gl") || m.Contains("account")))
        {
            op = "gl.balance.unusual";
            tools.Add(op);
            return true;
        }
        return false;
    }

    private static bool TryVat(string message, string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!m.Contains("vat") && !m.Contains("tax") && !m.Contains("zero-rated") && !m.Contains("zero rated"))
            return false;

        if (m.Contains("sales") && m.Contains("discount") && !m.Contains("vat"))
            return false;

        if (m.Contains("output") && (m.Contains("vat") || m.Contains("tax")))
        {
            op = "vat.output";
            tools.Add(op);
            return true;
        }

        if (m.Contains("input") && (m.Contains("vat") || m.Contains("tax")))
        {
            op = "vat.input";
            tools.Add(op);
            return true;
        }

        if (QueryAggregationMode.IsAggregationQuery(m) && m.Contains("vat"))
        {
            if (m.Contains("output"))
            {
                op = "vat.output";
                tools.Add(op);
                return true;
            }
            if (m.Contains("input"))
            {
                op = "vat.input";
                tools.Add(op);
                return true;
            }
        }

        if (m.Contains("reconcil") && (m.Contains("vat") || m.Contains("tax")))
        {
            op = "vat.reconcile";
            tools.Add(op);
            return true;
        }

        if (m.Contains("match") && m.Contains("vat") && m.Contains("control"))
        {
            op = "vat.reconcile";
            tools.Add(op);
            return true;
        }

        if (QueryAggregationMode.IsAggregationQuery(m) && m.Contains("output"))
        {
            op = "vat.output";
            tools.Add(op);
            return true;
        }

        if (QueryAggregationMode.IsAggregationQuery(m) && m.Contains("input"))
        {
            op = "vat.input";
            tools.Add(op);
            return true;
        }

        if (m.Contains("payable") || m.Contains("estimate"))
        {
            op = "vat.payable.estimate";
            tools.Add(op);
            return true;
        }

        if (m.Contains("trend"))
        {
            op = "vat.trend";
            tools.Add(op);
            return true;
        }

        if (m.Contains("zero") || m.Contains("exempt"))
        {
            op = "vat.zero.rated";
            tools.Add(op);
            return true;
        }

        if (m.Contains("missing") || m.Contains("no vat"))
        {
            op = "vat.missing";
            tools.Add(op);
            return true;
        }

        if (m.Contains("suspicious") || m.Contains("unusual") || m.Contains("anomal") ||
            (m.Contains("why") && m.Contains("vat") && !m.Contains("top") && !m.Contains("customer")) ||
            (m.Contains("vat") && (m.Contains("increased") || m.Contains("increase")) && !m.Contains("output") && !m.Contains("input")))
        {
            op = "vat.anomalies";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("top") || m.Contains("highest")) && m.Contains("customer"))
        {
            op = "vat.by.account.top";
            parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();
            tools.Add(op);
            return true;
        }

        if (m.Contains("summary") || QueryAggregationMode.IsAggregationQuery(m))
        {
            op = "vat.summary";
            tools.Add(op);
            return true;
        }

        return false;
    }

    private static bool TryBank(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!m.Contains("bank") && !m.Contains("cashbook") && !m.Contains("cash book") &&
            !m.Contains("cash movement") && !m.Contains("daily cash"))
            return false;

        if (m.Contains("daily") || m.Contains("inflow") || m.Contains("outflow") || m.Contains("cash movement"))
        {
            op = "bank.daily.cash";
            tools.Add(op);
            return true;
        }

        if (m.Contains("unusual") || m.Contains("suspicious"))
        {
            op = "bank.unusual";
            tools.Add(op);
            return true;
        }

        op = "bank.cashbook";
        tools.Add(op);
        return true;
    }

    private static bool TryGlAudit(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!IsGlContext(m) && !m.Contains("journal"))
            return false;

        if (m.Contains("backdat") || m.Contains("posted late"))
        {
            op = "gl.transaction.backdated";
            tools.Add(op);
            return true;
        }

        if (m.Contains("round") && m.Contains("journal"))
        {
            op = "gl.journal.round";
            tools.Add(op);
            return true;
        }

        if (m.Contains("duplicate") && m.Contains("journal"))
        {
            op = "gl.journal.duplicate";
            tools.Add(op);
            return true;
        }

        if (m.Contains("month-end") || m.Contains("period end") ||
            (m.Contains("period") && m.Contains("end")))
        {
            op = "gl.journal.periodend";
            tools.Add(op);
            return true;
        }

        if (m.Contains("manual") && m.Contains("journal") && !m.Contains("revers"))
        {
            op = "gl.journal.manual";
            tools.Add(op);
            return true;
        }

        if (m.Contains("user") && m.Contains("journal"))
        {
            op = "gl.journal.users.top";
            tools.Add(op);
            return true;
        }

        if (m.Contains("trial balance") && (m.Contains("suspicious") || m.Contains("anomal")))
        {
            op = "gl.trialbalance.anomaly";
            tools.Add(op);
            return true;
        }

        return false;
    }

    private static bool TryGlAnalytics(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        var unusualBalance = (m.Contains("unusual") || m.Contains("abnormal")) && m.Contains("balance");
        if (!m.Contains("expense") && !m.Contains("ratio") && !m.Contains("current ratio") && !unusualBalance)
            return false;

        if (m.Contains("current ratio") || m.Contains("quick ratio"))
        {
            op = "gl.ratios";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("unusual") || m.Contains("abnormal")) && (m.Contains("balance") || m.Contains("gl account")))
        {
            op = "gl.balance.unusual";
            tools.Add(op);
            return true;
        }

        if (m.Contains("variance") || (m.Contains("abnormal") && m.Contains("expense")) || m.Contains("changed significantly"))
        {
            op = "gl.expense.variance";
            tools.Add(op);
            return true;
        }

        if (m.Contains("trend") || m.Contains("month over month"))
        {
            op = "gl.expense.trend";
            tools.Add(op);
            return true;
        }

        if (m.Contains("top") || m.Contains("highest") || m.Contains("largest"))
        {
            op = "gl.expense.top";
            parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();
            tools.Add(op);
            return true;
        }

        return false;
    }

    private static bool TryInventoryGl(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (m.Contains("stock adjustment") || (m.Contains("adjustment") && m.Contains("stock")))
        {
            if (m.Contains("top") || m.Contains("largest"))
            {
                op = "inventory.adjustment.top";
                tools.Add(op);
                return true;
            }
        }

        if (m.Contains("inventory") && m.Contains("gl") && m.Contains("credit") && m.Contains("balance sheet"))
        {
            op = "inventory.bs.negative_ledgers";
            tools.Add(op);
            return true;
        }

        return false;
    }
}
