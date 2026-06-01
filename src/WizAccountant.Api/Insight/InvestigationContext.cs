using System.Text.Json;
using System.Text.RegularExpressions;

namespace WizAccountant.Api.Insight;

/// <summary>Lightweight follow-up investigation state across chat turns (SAGE-CONSOLIDATION-001).</summary>
internal sealed class InvestigationContext
{
    public string? LastOperation { get; init; }
    public string? WarehouseCode { get; init; }
    public string? CustomerCode { get; init; }
    public string? SupplierCode { get; init; }
    public string? StockCode { get; init; }
    public string? ActiveTopic { get; init; }

    public static InvestigationContext? FromPriorAssistantMessage(string? toolsUsedJson, string? lastReplySnippet)
    {
        if (string.IsNullOrWhiteSpace(toolsUsedJson))
            return null;

        try
        {
            var tools = JsonSerializer.Deserialize<List<string>>(toolsUsedJson);
            if (tools is null || tools.Count == 0)
                return null;

            var lastOp = tools.LastOrDefault(t => !t.StartsWith("intent:", StringComparison.Ordinal) &&
                                                  !t.StartsWith("domain:", StringComparison.Ordinal) &&
                                                  !t.StartsWith("businessProcess:", StringComparison.Ordinal) &&
                                                  !t.StartsWith("handler:", StringComparison.Ordinal) &&
                                                  !t.StartsWith("route:", StringComparison.Ordinal));

            return new InvestigationContext
            {
                LastOperation = lastOp,
                ActiveTopic = InferTopic(lastOp, lastReplySnippet)
            };
        }
        catch
        {
            return null;
        }
    }

    public void ApplyFollowUp(string message, Dictionary<string, string> parameters)
    {
        var m = message.ToLowerInvariant();

        var wh = Regex.Match(message, @"(?:warehouse|whse)\s*[#:]?\s*(\w+)", RegexOptions.IgnoreCase);
        if (wh.Success)
            parameters["warehouseCode"] = wh.Groups[1].Value;

        var cust = Regex.Match(message, @"(?:customer|for)\s+([A-Z0-9][A-Z0-9\-]{1,15})\b", RegexOptions.IgnoreCase);
        if (cust.Success)
            parameters["customerCode"] = cust.Groups[1].Value.ToUpperInvariant();

        var stock = Regex.Match(message, @"(?:item|stock)\s+([A-Z0-9][A-Z0-9\-]{1,20})\b", RegexOptions.IgnoreCase);
        if (stock.Success)
            parameters["stockCode"] = stock.Groups[1].Value.ToUpperInvariant();

        if (LastOperation is not null)
            parameters["investigationPriorOp"] = LastOperation;

        if (ActiveTopic is not null)
            parameters["investigationTopic"] = ActiveTopic;

        if (IsWarehouseFollowUp(m) && parameters.ContainsKey("warehouseCode") &&
            LastOperation is "inventory.gl.explain" or "inventory.gl.reconcile" or "inventory.warehouse.reconcile")
        {
            parameters["followUp"] = "warehouse-detail";
        }

        if (IsTransactionFollowUp(m) && !string.IsNullOrEmpty(StockCode))
            parameters["stockCode"] = StockCode;
        else if (IsTransactionFollowUp(m) && parameters.TryGetValue("stockCode", out _))
            parameters["followUp"] = "transactions";
    }

    private static bool IsWarehouseFollowUp(string m) =>
        m.Contains("warehouse") && (m.Contains("detail") || m.Contains("show") || m.Contains("drill"));

    private static bool IsTransactionFollowUp(string m) =>
        m.Contains("transaction") || m.Contains("related posting") || m.Contains("drilldown");

    private static string? InferTopic(string? lastOp, string? reply)
    {
        if (string.IsNullOrEmpty(lastOp))
            return null;
        if (lastOp.Contains("inventory", StringComparison.Ordinal))
            return "inventory";
        if (lastOp.Contains("vat", StringComparison.Ordinal))
            return "vat";
        if (lastOp.Contains("bank", StringComparison.Ordinal) || lastOp.Contains("treasury", StringComparison.Ordinal))
            return "treasury";
        if (lastOp.Contains("customer.payment", StringComparison.Ordinal))
            return "payment-behavior";
        if (lastOp.Contains("reconcile", StringComparison.Ordinal) || lastOp.Contains(".gl.", StringComparison.Ordinal))
            return "reconciliation";
        if (reply?.Contains("variance", StringComparison.OrdinalIgnoreCase) == true)
            return "variance";
        return lastOp;
    }
}
