namespace WizAccountant.Api.Insight;

/// <summary>Reconciliation intelligence routing (SAGE-TRAIN-006).</summary>
internal static class ReconciliationChatMatcher
{
    public static bool TryRoute(
        string message,
        string m,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;
        parameters["message"] = message;

        if (TryInventoryDrilldown(message, m, parameters, tools, out operation))
            return true;

        if (TryInventory(message, m, parameters, tools, out operation))
            return true;

        if (TryFixedAssets(m, parameters, tools, out operation))
            return true;

        if (TryAr(m, parameters, tools, out operation))
            return true;

        if (TryAp(m, parameters, tools, out operation))
            return true;

        if (TryVat(m, parameters, tools, out operation))
            return true;

        if (TryBank(m, parameters, tools, out operation))
            return true;

        return false;
    }

    private static bool IsReconContext(string m) =>
        m.Contains("reconcil") || m.Contains("not matching") || m.Contains("doesn't match") ||
        m.Contains("does not match") || m.Contains("not balance") || m.Contains("variance") ||
        m.Contains("mismatch") || (m.Contains("match") && (m.Contains("control") || m.Contains("gl"))) ||
        (m.Contains("why") && (m.Contains("not matching") || m.Contains("inventory") || m.Contains("bank")));

    private static bool IsArContext(string m) =>
        m.Contains("ar ") || m.StartsWith("ar ") || m.Contains(" receivable") || m.Contains("debtor") ||
        m.Contains("customer") && !m.Contains("supplier");

    private static bool IsApContext(string m) =>
        !m.Contains("vat") &&
        (m.Contains("ap ") || m.StartsWith("ap ") ||
         (m.Contains("payable") && !m.Contains("vat")) ||
         m.Contains("creditor") || m.Contains("supplier"));

    private static bool IsInvContext(string m) =>
        m.Contains("inventory") || m.Contains("stock") || m.Contains("valuation") || m.Contains("warehouse");

    private static bool TryInventoryDrilldown(string message, string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!m.Contains("detail") && !m.Contains("drill"))
            return false;

        var match = System.Text.RegularExpressions.Regex.Match(
            message,
            @"\bfor\s+([A-Z0-9][A-Z0-9\-]{1,20})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
            parameters["stockCode"] = match.Groups[1].Value.ToUpperInvariant();

        if (!parameters.ContainsKey("stockCode"))
            return false;

        op = "inventory.item.drilldown";
        tools.Add(op);
        return true;
    }

    private static bool TryInventory(string message, string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!IsInvContext(m))
            return false;

        if (m.Contains("credit balance") && m.Contains("gl") && !m.Contains("reconcil"))
        {
            op = "inventory.bs.negative_ledgers";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("why") || m.Contains("explain") || m.Contains("root cause")) &&
            (m.Contains("not matching") || m.Contains("mismatch") || m.Contains("inventory")))
        {
            op = "inventory.gl.explain";
            tools.Add(op);
            return true;
        }

        if (m.Contains("warehouse") && (IsReconContext(m) || m.Contains("mismatch") || m.Contains("causing")))
        {
            op = "inventory.warehouse.reconcile";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("stock group") || m.Contains("stockgroup")) && (IsReconContext(m) || m.Contains("variance") || m.Contains("mismatch")))
        {
            op = "inventory.stockgroup.reconcile";
            tools.Add(op);
            return true;
        }

        if (SageChatDomain.TryInventoryGlReconcile(m, parameters, tools, out op))
            return true;

        return false;
    }

    private static bool TryAr(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!IsArContext(m))
            return false;

        if (m.Contains("unallocated") && (m.Contains("ar") || m.Contains("receivable")))
        {
            op = "ar.unallocated";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("causing") || m.Contains("contributor") || m.Contains("contributing") ||
             m.Contains("which customer")) &&
            (m.Contains("variance") || m.Contains("mismatch")))
        {
            op = "ar.variance.contributors";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("aging") || m.Contains("open item") || m.Contains("outstanding")) &&
            (m.Contains("control") || m.Contains("debtor") || m.Contains("gl")) &&
            (m.Contains("match") || m.Contains("reconcil")))
        {
            op = "ar.gl.reconcile";
            tools.Add(op);
            return true;
        }

        if (m.Contains("reconcil") && (m.Contains("debtor") || m.Contains("control")))
        {
            op = "ar.gl.reconcile";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("ar") || m.Contains("receivable")) && m.Contains("match") && m.Contains("gl"))
        {
            op = "ar.gl.reconcile";
            tools.Add(op);
            return true;
        }

        return false;
    }

    private static bool TryAp(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!IsApContext(m))
            return false;

        if (m.Contains("unallocated") && (m.Contains("supplier") || m.Contains("ap")))
        {
            op = "ap.unallocated";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("causing") || m.Contains("contributor") || m.Contains("contributing") ||
             m.Contains("which supplier")) &&
            (m.Contains("variance") || m.Contains("mismatch")))
        {
            op = "ap.variance.contributors";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("aging") || m.Contains("outstanding") || m.Contains("payable")) &&
            (m.Contains("control") || m.Contains("creditor") || m.Contains("gl")) &&
            (m.Contains("match") || m.Contains("reconcil")))
        {
            op = "ap.gl.reconcile";
            tools.Add(op);
            return true;
        }

        if (m.Contains("reconcil") && (m.Contains("creditor") || m.Contains("control")))
        {
            op = "ap.gl.reconcile";
            tools.Add(op);
            return true;
        }

        return false;
    }

    private static bool TryVat(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!m.Contains("vat") && !m.Contains("tax"))
            return false;

        if (m.Contains("missing") || m.Contains("no vat"))
        {
            op = "vat.missing";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("payable") || m.Contains("control")) && m.Contains("match") && m.Contains("vat"))
        {
            op = "vat.reconcile";
            tools.Add(op);
            return true;
        }

        if ((m.Contains("causing") || m.Contains("which invoice") || m.Contains("contributor")) &&
            (m.Contains("mismatch") || m.Contains("variance")))
        {
            op = "vat.variance.contributors";
            tools.Add(op);
            return true;
        }

        if (m.Contains("reconcil") || (m.Contains("control") && m.Contains("match")))
        {
            op = "vat.reconcile";
            tools.Add(op);
            return true;
        }

        return false;
    }

    private static bool TryBank(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        var bankCtx = m.Contains("bank") || m.Contains("cashbook") || m.Contains("cash book");

        if (m.Contains("unmatched") || (bankCtx && m.Contains("unalloc")))
        {
            op = "bank.unmatched";
            tools.Add(op);
            return true;
        }

        if (m.Contains("unpresented") || m.Contains("cheque") || m.Contains("check"))
        {
            op = "bank.cheques.unpresented";
            tools.Add(op);
            return true;
        }

        if (m.Contains("outstanding") && m.Contains("deposit"))
        {
            op = "bank.deposits.outstanding";
            tools.Add(op);
            return true;
        }

        if (bankCtx && (m.Contains("not balancing") || m.Contains("reconcil") || IsReconContext(m)))
        {
            op = "bank.reconcile.variance";
            tools.Add(op);
            return true;
        }

        return false;
    }

    private static bool TryFixedAssets(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = null;
        if (!m.Contains("depreciation") && !m.Contains("fixed asset") && !m.Contains("fa "))
            return false;

        if ((m.Contains("causing") || m.Contains("which asset") || m.Contains("contributor")) &&
            (m.Contains("variance") || m.Contains("mismatch")))
        {
            op = "fa.variance.contributors";
            tools.Add(op);
            return true;
        }

        if (m.Contains("match") || m.Contains("reconcil"))
        {
            op = "fa.depreciation.reconcile";
            tools.Add(op);
            return true;
        }

        return false;
    }
}
