using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>Root-cause narrative for inventory vs GL mismatch (SAGE-TRAIN-006 Scope G).</summary>
internal static class InventoryGlExplainHandler
{
    public const string QuerySerial = "SAGE-INV-GL-EXPLAIN-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var reconJson = InventoryGlReconcileHandler.Execute(companyConnectionString, parameters);
        using var doc = JsonDocument.Parse(reconJson);
        var root = doc.RootElement;

        var subledger = root.TryGetProperty("inventoryValuation", out var v) ? v.GetDecimal() : 0m;
        var glTotal = root.TryGetProperty("balanceSheetStockValue", out var g) ? g.GetDecimal() : 0m;
        var diff = subledger - glTotal;
        var matches = root.TryGetProperty("matches", out var m) && m.ValueKind == JsonValueKind.True;

        var contributors = new List<object>();

        if (root.TryGetProperty("mainVariance", out var mv) && mv.ValueKind == JsonValueKind.Object)
        {
            contributors.Add(new
            {
                rank = 1,
                contributorType = "glAccount",
                code = mv.TryGetProperty("glAccount", out var ga) ? ga.GetString() : "",
                name = mv.TryGetProperty("glAccountName", out var gn) ? gn.GetString() : "",
                variance = mv.TryGetProperty("difference", out var d) ? d.GetDecimal() : 0m
            });
        }

        if (root.TryGetProperty("stockGroupValuation", out var sg) && sg.ValueKind == JsonValueKind.Array)
        {
            var rank = contributors.Count + 1;
            foreach (var el in sg.EnumerateArray().Take(5))
            {
                contributors.Add(new
                {
                    rank,
                    contributorType = "stockGroup",
                    code = el.TryGetProperty("stockGroup", out var g1) ? g1.GetString() : "",
                    name = el.TryGetProperty("stockGroupName", out var g2) ? g2.GetString() : "",
                    valuation = el.TryGetProperty("inventoryValuation", out var val) ? val.GetDecimal() : 0m
                });
                rank++;
            }
        }

        var finding = matches
            ? "Inventory valuation matches balance sheet — no material variance to explain."
            : $"Inventory mismatch of {diff:N2}: valuation {subledger:N2} vs GL {glTotal:N2}. " +
              "Review GL accounts, stock groups, and warehouses below.";

        return ReconcileEnvelope.Build(
            QuerySerial,
            "Inventory reconciliation explainability",
            subledger,
            glTotal,
            contributors,
            finding,
            matches,
            new
            {
                explanationSteps = new[]
                {
                    "Compared inventory valuation SQL to PostGL on distinct stock GL accounts.",
                    "Ranked GL accounts and stock groups with largest valuation differences.",
                    "Use inventory.item.drilldown for a specific item code follow-up."
                },
                parentQuery = InventoryGlReconcileHandler.QuerySerial
            });
    }
}
