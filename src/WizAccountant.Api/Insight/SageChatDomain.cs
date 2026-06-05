namespace WizAccountant.Api.Insight;

/// <summary>Maps user questions to Sage 200 layers (see DOCS/SAGE-200-DATABASE-LAYERS.md).</summary>
internal static class SageChatDomain
{
    internal enum Layer
    {
        Unknown,
        AccountsReceivable,
        AccountsPayable,
        Inventory,
        GeneralLedger,
        FixedAssets,
        Manufacturing,
        Dashboard
    }

    public static Layer Detect(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return Layer.Unknown;

        var m = messageLower;

        if (m.Contains("fixed asset") || m.Contains("depreciation") || m.Contains("asset type"))
            return Layer.FixedAssets;

        if (m.Contains("bom") || m.Contains("manufactur") || m.Contains("wip") || m.Contains("dstk"))
            return Layer.Manufacturing;

        if (m.Contains("balance sheet") || m.Contains("trial balance") || m.Contains("postgl") ||
            m.Contains("general ledger") || m.Contains(" gl ") || m.StartsWith("gl "))
            return Layer.GeneralLedger;

        if (m.Contains("inventory valuation") || m.Contains("stock group") || m.Contains("stkitem") ||
            m.Contains("valuation") || m.Contains("stock item") || m.Contains("warehouse"))
            return Layer.Inventory;

        if (m.Contains("supplier") || m.Contains("payable") || m.Contains("purchase"))
            return Layer.AccountsPayable;

        if (m.Contains("dashboard") || m.Contains("kpi") || m.Contains("summary"))
            return Layer.Dashboard;

        if (m.Contains("customer") || m.Contains("invoice") || m.Contains("receivable") || m.Contains(" ar ") ||
            m.Contains("credit note"))
            return Layer.AccountsReceivable;

        return Layer.Unknown;
    }

    public static string? TryBuildEducationalReply(string message)
    {
        var m = message.ToLowerInvariant();

        if (IsInventoryGlReconciliationQuestion(m))
            return null;

        if (m.Contains("depreciation") && (m.Contains("march") || m.Contains("january") || m.Contains("february") || m.Contains("wrong")))
        {
            return "Fixed asset depreciation: compare PostGL (Id=JL, Reference like FA%) to _btblFAGLBatchAssetValues before assuming a month is wrong. " +
                   "Investigations often show GL overstated vs FA batch, not the odd month. FA reads are not in chat yet — use Sage FA depreciation reports.";
        }

        if (m.Contains("postst") && m.Contains("valuation"))
        {
            return "Do not value inventory by summing PostST Debit − Credit. Sage Valuation uses quantity balance × cost from costing functions. " +
                   "Use inventory valuation questions in chat (inventoryitem.list) or Sage Inventory Valuation by Date report.";
        }

        return null;
    }

    public static bool IsInventoryGlReconciliationQuestion(string m)
    {
        if (m.Contains("balance sheet") && (m.Contains("negative") || m.Contains("credit balance")) &&
            (m.Contains("stock") || m.Contains("inventory")) && !m.Contains("reconcil") && !m.Contains("valuation"))
            return false;

        var hasInventory = m.Contains("inventory") || m.Contains("valuation") || m.Contains("stock value") ||
                           (m.Contains("stock") && !m.Contains("stock count"));
        var hasGl = m.Contains("balance sheet") || m.Contains("stock value") || m.Contains("trial balance") ||
                    (m.Contains("gl") && m.Contains("inventory"));
        if (!hasInventory || !hasGl)
            return false;

        var asksReconcile = m.Contains("match") || m.Contains("mismatch") || m.Contains("not matching") ||
                            m.Contains("doesn't match") || m.Contains("does not match") || m.Contains("differ") ||
                            m.Contains("reconcil") || m.Contains("compare") || m.Contains("why") ||
                            m.Contains("same") || m.Contains("align") ||
                            (m.StartsWith("is ") && m.Contains("matching"));

        if (asksReconcile)
            return true;

        // Fix-workflow triggers (DOCS/Sage_AI_Inventory_Fix_Workflow_Patch.md §12).
        return InventoryFixWorkflow.IsFixWorkflowRequest(m);
    }

    public static bool TryInventoryGlReconcile(
        string messageLower,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string operation)
    {
        operation = "inventory.gl.reconcile";
        if (!IsInventoryGlReconciliationQuestion(messageLower))
            return false;

        if (InventoryFixWorkflow.IsFixWorkflowRequest(messageLower))
            parameters["fixWorkflow"] = "true";

        parameters["freshRun"] = DateTimeOffset.UtcNow.ToString("O");
        tools.Add(operation);
        return true;
    }

    public static bool TryGlTransactionList(string messageLower, Dictionary<string, string> parameters, List<string> tools, out string operation)
    {
        operation = "gltransaction.list";
        if (IsInventoryGlReconciliationQuestion(messageLower))
            return false;

        var m = messageLower;
        if (m.Contains("gl transaction") || m.Contains("general ledger") || m.Contains("trial balance") ||
            m.Contains("postgl") || (m.Contains("gl ") && (m.Contains("list") || m.Contains("show") || m.Contains("transaction"))))
        {
            parameters["top"] = "50";
            tools.Add(operation);
            return true;
        }

        return false;
    }

    public static string BuildUnmatchedReply(string siteName, string message)
    {
        var layer = Detect(message.ToLowerInvariant());
        var examples = layer switch
        {
            Layer.AccountsReceivable =>
                "Try: \"top 5 customers with oldest aged debit balances\", \"which customer has highest unpaid invoices\", \"get me sales invoices that are unpaid\".",
            Layer.AccountsPayable =>
                "Try: \"suppliers with balance over 50000\", \"open items for SUP001\".",
            Layer.Inventory =>
                "Try: \"which items have valuation over 100000\", \"list inventory\".\n" +
                "Note: Sage inventory valuation uses costing tables (_evInvCostTracking), not a simple sum of PostST.",
            Layer.GeneralLedger =>
                "Try: \"show GL transactions for April\", \"trial balance\" (sample GL rows via gltransaction.list).\n" +
                "Note: Balance Sheet comes from PostGL; compare to subledger reports before assuming GL is wrong.",
            Layer.FixedAssets =>
                "Fixed asset depreciation in Sage uses FA batch tables (_btblFAGLBatchAssetValues), not PostGL alone. " +
                "This is not fully exposed in chat yet — use Sage FA reports or ask for a future allowlisted read.",
            Layer.Manufacturing =>
                "Manufacturing/BOM uses PostST + process tables. Not fully exposed in chat yet — use Sage manufacturing reports.",
            Layer.Dashboard =>
                "Try: \"show dashboard\".",
            _ =>
                "Try: \"show dashboard\", \"is inventory valuation matching balance sheet\", \"which customer has highest unpaid invoices\", \"items with valuation over 100000\"."
        };

        return $"I can help with live Sage reads on {siteName} (AR, AP, inventory, GL, dashboard). {examples}\n" +
               $"[{InsightChatInfo.Version}] I only read data — no posting. {GuardrailSnippet}";
    }

    public static string? LayerFootnote(string operation)
    {
        return operation switch
        {
            "salesinvoice.discount.count" =>
                "Sales invoices (InvNum): count of distinct invoices with header/line discount in the requested year — not open AR lines.",
            CreditNoteChatHelper.SalesCreditNoteCountOperation =>
                "Sales credit notes (InvNum DocType 1): count and total value in period — not customer credit balances.",
            "customer.aged.top" =>
                "AR aging: ranked by oldest open invoice date per customer (Outstanding > 0), not Customer.List master dump.",
            "customer.credit.balances" =>
                "AR: customers with credit (negative) master balance — digest AR credit balance queries.",
            "supplier.aged.top" =>
                "AP aging: ranked by oldest open supplier balance, not Supplier.List master dump.",
            "customer.openitems" or "customer.unpaid.summary" =>
                "Sage layer: AR subledger (CustomerTransaction, Outstanding <> 0). Not the same as PostGL OInv rows alone.",
            "inventoryitem.list" =>
                "Sage layer: inventory costing (item valuation from SDK). Balance Sheet inventory comes from PostGL — they can differ until reconciled.",
            "inventory.bs.negative_ledgers" =>
                "PostGL net balance on distinct inventory stock GL accounts (GrpTbl.StockAccLink); negative = credit balance on Balance Sheet.",
            "inventory.gl.reconcile" =>
                "Compared distinct PostGL inventory GL totals vs Sage valuation SQL (SAGE-INVVAL-RECON-CANONICAL-001).",
            "gltransaction.list" =>
                "Sage layer: general ledger (PostGL-style postings). Use for GL detail, not stock valuation.",
            "supplier.openitems" =>
                "Sage layer: AP subledger (SupplierTransaction, Outstanding <> 0).",
            _ => null
        };
    }

    private const string GuardrailSnippet = "Ask an approver in Phase 3 for journals or payments.";
}
