using System.Text.Json;

namespace WizAccountant.Api.Insight;

/// <summary>Fix-workflow intent and response (DOCS/Sage_AI_Inventory_Fix_Workflow_Patch.md).</summary>
internal static class InventoryFixWorkflow
{
    public const string IntentName = "inventory_reconciliation_fix_workflow";

    public static bool WantsFixWorkflow(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return false;
        return IsFixWorkflowRequest(userMessage.ToLowerInvariant());
    }

    public static bool IsFixWorkflowRequest(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        var fixWords = messageLower.Contains("can you fix") || messageLower.Contains("please fix") ||
                       messageLower.Contains("fix it") || messageLower.Contains("fix this") ||
                       messageLower.Contains("datafix") || messageLower.Contains("make it match") ||
                       messageLower.Contains("adjust it") || messageLower.Contains("repair mismatch") ||
                       messageLower.Contains("fix") ||
                       messageLower.Contains("resolve") || messageLower.Contains("correct");

        if (!fixWords)
            return false;

        return messageLower.Contains("inventory") || messageLower.Contains("valuation") ||
               messageLower.Contains("balance sheet") || messageLower.Contains("stock value") ||
               messageLower.Contains("stock valuation") || (messageLower.Contains("stock") && messageLower.Contains("gl"));
    }

    public static IReadOnlyList<string> BuildOpeningLines()
    {
        return new[]
        {
            "Finding:",
            "I will not post a fix yet. I will first run the reconciliation and identify the exact mismatch source.",
            "",
            "Action:",
            "Running inventory valuation vs Balance Sheet reconciliation using Sage SQL valuation logic, not SDK.",
            "",
            "Next:",
            "If a real mismatch exists, I will drill down by GL account, stock group, and item/warehouse, then prepare a rollback-only datafix preview."
        };
    }

    public static IReadOnlyList<string> BuildSanityFailureLines(JsonElement root, InventoryReconcileValidator.ValidationResult validation)
    {
        var gl = GetDecimal(root, "balanceSheetStockValue");
        var val = GetDecimal(root, "inventoryValuation");

        return new List<string>
        {
            "",
            "Finding:",
            "The reconciliation result is not reliable because the valuation side appears incomplete.",
            "",
            "Balance Sheet Stock Value:",
            gl.ToString("N2"),
            "",
            "Returned Valuation:",
            val.ToString("N2"),
            "",
            "Issue:",
            validation.FailureReason ?? "Valuation failed sanity validation.",
            "This valuation is suspiciously low and may indicate partial SQL execution or incomplete costing data — not a valid mismatch.",
            "",
            "Action:",
            "I will not prepare a datafix until the valuation SQL returns a credible Sage Inventory Valuation total.",
            "Check connector SQL logs for _evInvCostTracking / _efnLastCostByDatePerItem errors."
        };
    }

    public static IReadOnlyList<string> BuildValidMismatchFixPlan(JsonElement root)
    {
        var lines = new List<string>
        {
            "",
            "Fix Plan:",
            InferRootCause(root),
            "",
            "Drilldown completed:",
            "• GL accounts (detail rows in grid)",
            "• Stock groups (where valuation SQL returned groups)"
        };

        AppendTopVariances(lines, root);
        AppendStockGroupDrilldown(lines, root);

        lines.Add("");
        lines.Add("Datafix Status:");
        lines.Add("Preview only. No changes committed.");
        lines.Add("");
        lines.Add(BuildDatafixPreview(root));
        lines.Add("");
        lines.Add("To apply a live script in a future phase, you would type: APPROVE INVENTORY DATAFIX");
        lines.Add("(Insight is read-only today — no commit will run.)");

        return lines;
    }

    private static string InferRootCause(JsonElement root)
    {
        var gl = GetDecimal(root, "balanceSheetStockValue");
        var val = GetDecimal(root, "inventoryValuation");
        var valLines = root.TryGetProperty("valuationLineCount", out var vlc) && vlc.ValueKind == JsonValueKind.Number
            ? vlc.GetInt32()
            : 0;

        if (valLines == 0 && gl != 0)
            return "Likely cause: Cost tracking issue — valuation SQL returned no item lines while GL inventory is non-zero.";

        if (gl > 0 && val > 0 && val / gl < 0.5m)
            return "Likely cause: Cost tracking issue or incomplete valuation scope (warehouses/items excluded from costing view).";

        var accountsWithGlOnly = CountAccountsWhere(root, glNonZero: true, valZero: true);
        if (accountsWithGlOnly >= 2)
            return "Likely cause: Manual/direct GL journal or opening migration mismatch — GL balance on stock account(s) without matching valuation.";

        if (root.TryGetProperty("stockGroupValuation", out var sg) && sg.GetArrayLength() > 0)
            return "Likely cause: Stock group / cost tracking mismatch — drill into stock groups and main variance GL account first.";

        return "Likely cause: Investigate main variance GL account (PostGL vs Inventory Valuation by Date for that account).";
    }

    private static string BuildDatafixPreview(JsonElement root)
    {
        var acct = "";
        var name = "";
        if (root.TryGetProperty("mainVariance", out var mv) && mv.ValueKind == JsonValueKind.Object)
        {
            acct = mv.TryGetProperty("glAccount", out var ga) ? ga.GetString() ?? "" : "";
            name = mv.TryGetProperty("glAccountName", out var gn) ? gn.GetString() ?? "" : "";
        }

        return $"""
                  Datafix preview (rollback only — do not run without approval):
                  BEGIN TRANSACTION;
                  -- Investigate PostGL vs costing for GL {acct} ({name})
                  -- Example: compare PostGL balance to _evInvCostTracking valuation by stock group/item
                  -- Proposed correction depends on root cause (journal reversal, costing adjustment, stock group remap)
                  ROLLBACK TRANSACTION;
                  """;
    }

    private static int CountAccountsWhere(JsonElement root, bool glNonZero, bool valZero)
    {
        if (!root.TryGetProperty("accounts", out var accounts) || accounts.ValueKind != JsonValueKind.Array)
            return 0;

        var count = 0;
        foreach (var a in accounts.EnumerateArray())
        {
            var gl = a.TryGetProperty("balanceSheet", out var g) && g.ValueKind == JsonValueKind.Number ? g.GetDecimal() : 0;
            var val = a.TryGetProperty("inventoryValuation", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0;
            if (glNonZero && Math.Abs(gl) > 0.01m && valZero && Math.Abs(val) <= 0.01m)
                count++;
        }

        return count;
    }

    private static void AppendTopVariances(List<string> lines, JsonElement root)
    {
        if (!root.TryGetProperty("accounts", out var accounts) || accounts.ValueKind != JsonValueKind.Array)
            return;

        var top = accounts.EnumerateArray()
            .Select(a => new
            {
                Code = a.TryGetProperty("glAccount", out var c) ? c.GetString() : "",
                Name = a.TryGetProperty("glAccountName", out var n) ? n.GetString() : "",
                Diff = a.TryGetProperty("difference", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDecimal() : 0m
            })
            .Where(x => Math.Abs(x.Diff) > 0.01m)
            .OrderByDescending(x => Math.Abs(x.Diff))
            .Take(5)
            .ToList();

        if (top.Count == 0)
            return;

        lines.Add("");
        lines.Add("Main variance accounts:");
        foreach (var row in top)
            lines.Add($"• {row.Code} — {row.Name}: difference {row.Diff:N2}");
    }

    private static void AppendStockGroupDrilldown(List<string> lines, JsonElement root)
    {
        if (!root.TryGetProperty("stockGroupValuation", out var groups) || groups.ValueKind != JsonValueKind.Array)
            return;

        var top = groups.EnumerateArray().Take(5).ToList();
        if (top.Count == 0)
            return;

        lines.Add("");
        lines.Add("Stock groups (valuation drilldown):");
        foreach (var g in top)
        {
            var sg = g.TryGetProperty("stockGroup", out var s) ? s.GetString() : "";
            var name = g.TryGetProperty("stockGroupName", out var n) ? n.GetString() : "";
            var val = g.TryGetProperty("inventoryValuation", out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDecimal().ToString("N2")
                : "";
            lines.Add($"• {sg} — {name}: valuation {val}");
        }
    }

    private static decimal GetDecimal(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDecimal();
        return 0;
    }
}
