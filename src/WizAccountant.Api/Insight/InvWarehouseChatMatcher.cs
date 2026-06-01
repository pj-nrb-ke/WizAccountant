using System.Text.RegularExpressions;

namespace WizAccountant.Api.Insight;

/// <summary>Inventory + Warehouse analytical routing (SAGE-TRAIN-004).</summary>
internal static class InvWarehouseChatMatcher
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

        if (ChatIntentMatcher.IsInventoryBsNegativeLedgersQuery(m))
            return false;

        if (SageChatDomain.IsInventoryGlReconciliationQuestion(m))
            return false;

        if (TryWarehouseValue(m, parameters, tools, out operation))
            return true;
        if (TryWarehouseNegative(m, parameters, tools, out operation))
            return true;
        if (TryWarehouseNonMoving(m, parameters, tools, out operation))
            return true;
        if (TryWarehouseTransfer(m, parameters, tools, out operation))
            return true;
        if (TryWarehouseDiscrepancy(m, parameters, tools, out operation))
            return true;
        if (TryNegativeQty(m, parameters, tools, out operation))
            return true;
        if (TryNegativeValuation(m, parameters, tools, out operation))
            return true;
        if (TrySlowMoving(m, parameters, tools, out operation))
            return true;
        if (TryNonMoving(m, parameters, tools, out operation))
            return true;
        if (TryBelowReorder(m, parameters, tools, out operation))
            return true;
        if (TryOverstocked(m, parameters, tools, out operation))
            return true;
        if (TryValueTop(m, parameters, tools, out operation))
            return true;
        if (TryMovementTop(m, parameters, tools, out operation))
            return true;

        return false;
    }

    private static void ApplyCommonParameters(string message, string m, Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("warehouseCode", out var wh) && !string.IsNullOrEmpty(wh))
            parameters["warehouse"] = wh;

        var top = ChatIntentMatcher.ResolveTopCount(m, 0);
        if (top > 0)
            parameters["top"] = top.ToString();
        var year = ChatIntentMatcher.ExtractYearFromMessage(message);
        if (year.HasValue)
            parameters["year"] = year.Value.ToString();
        if (m.Contains("12 month") || m.Contains("365"))
            parameters["minDaysNoMove"] = "365";
        parameters["message"] = message;
    }

    private static bool HasStockContext(string m) =>
        m.Contains("stock") || m.Contains("inventory") || m.Contains("item") || m.Contains("warehouse");

    private static bool TryWarehouseValue(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "warehouse.value.summary";
        if (!m.Contains("warehouse") && !m.Contains("whse") && !parameters.ContainsKey("warehouseCode"))
            return false;
        if (!m.Contains("value") && !m.Contains("valuation") && !m.Contains("detail"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryWarehouseNegative(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "warehouse.negative.qty";
        if (!m.Contains("warehouse"))
            return false;
        if (!m.Contains("negative"))
            return false;
        if (m.Contains("balance sheet"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryWarehouseNonMoving(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "warehouse.nonmoving";
        if (!m.Contains("warehouse"))
            return false;
        if (!m.Contains("non moving") && !m.Contains("non-moving") && !m.Contains("not moved") && !m.Contains("no movement"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryWarehouseTransfer(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "warehouse.transfer.summary";
        if (!m.Contains("transfer"))
            return false;
        if (!m.Contains("warehouse") && !m.Contains("whse"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryWarehouseDiscrepancy(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "warehouse.discrepancy";
        if (!m.Contains("warehouse"))
            return false;
        if (!m.Contains("discrep") && !m.Contains("adjust") && !m.Contains("unusual"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryNegativeQty(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "inventory.negative.qty";
        if (!HasStockContext(m) || !m.Contains("negative"))
            return false;
        if (m.Contains("balance sheet") || m.Contains("ledger") || m.Contains("gl "))
            return false;
        if (m.Contains("valuation") && !m.Contains("quantity") && !m.Contains("qty") && !m.Contains("on hand"))
            return false;
        if (m.Contains("warehouse"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryNegativeValuation(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "inventory.negative.valuation";
        if (!HasStockContext(m) || !m.Contains("negative"))
            return false;
        if (!m.Contains("value") && !m.Contains("valuation"))
            return false;
        if (m.Contains("balance sheet"))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TrySlowMoving(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "inventory.slow.moving.top";
        if (!m.Contains("slow"))
            return false;
        if (!HasStockContext(m))
            return false;
        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 20).ToString();
        tools.Add(op);
        return true;
    }

    private static bool TryNonMoving(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "inventory.nonmoving";
        if (m.Contains("warehouse"))
            return false;
        if (!m.Contains("non moving") && !m.Contains("non-moving") && !m.Contains("not moved") && !m.Contains("no movement"))
            return false;
        if (!HasStockContext(m))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryBelowReorder(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "inventory.below.reorder";
        if (!m.Contains("reorder") && !m.Contains("replenish") && !m.Contains("minimum"))
            return false;
        if (!HasStockContext(m))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryOverstocked(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "inventory.overstocked";
        if (!m.Contains("overstock") && !m.Contains("excess"))
            return false;
        if (!HasStockContext(m))
            return false;
        tools.Add(op);
        return true;
    }

    private static bool TryValueTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "inventory.value.top";
        if (!HasStockContext(m))
            return false;
        if (!m.Contains("top") && !m.Contains("highest"))
            return false;
        if (!m.Contains("value") && !m.Contains("valuation"))
            return false;
        if (m.Contains("warehouse"))
            return false;
        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();
        tools.Add(op);
        return true;
    }

    private static bool TryMovementTop(string m, Dictionary<string, string> parameters, List<string> tools, out string? op)
    {
        op = "inventory.movement.top";
        if (!HasStockContext(m))
            return false;
        if (!m.Contains("top") && !m.Contains("most") && !m.Contains("highest"))
            return false;
        if (!m.Contains("mov") && !m.Contains("sell") && !m.Contains("sales"))
            return false;
        parameters["top"] = ChatIntentMatcher.ResolveTopCount(m, 10).ToString();
        tools.Add(op);
        return true;
    }
}

