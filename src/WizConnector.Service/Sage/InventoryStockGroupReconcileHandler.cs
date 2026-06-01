using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class InventoryStockGroupReconcileHandler
{
    public const string QuerySerial = "SAGE-INV-SG-RECON-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 15);
        var asOf = ReconcileSqlHelper.ParseAsOf(parameters);
        parameters["asOfDate"] = asOf.ToString("yyyy-MM-dd");

        var reconJson = InventoryGlReconcileHandler.Execute(companyConnectionString, parameters);
        using var doc = JsonDocument.Parse(reconJson);
        var root = doc.RootElement;
        var subledger = root.TryGetProperty("inventoryValuation", out var v) ? v.GetDecimal() : 0m;
        var glTotal = root.TryGetProperty("balanceSheetStockValue", out var g) ? g.GetDecimal() : 0m;

        var contributors = new List<object>();
        if (root.TryGetProperty("stockGroupValuation", out var sg) && sg.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var el in sg.EnumerateArray().Take(top))
            {
                i++;
                contributors.Add(new
                {
                    rank = i,
                    stockGroup = el.TryGetProperty("stockGroup", out var g1) ? g1.GetString() : "",
                    stockGroupName = el.TryGetProperty("stockGroupName", out var g2) ? g2.GetString() : "",
                    valuation = el.TryGetProperty("inventoryValuation", out var val) ? val.GetDecimal() : 0m
                });
            }
        }

        return ReconcileEnvelope.Build(
            QuerySerial,
            "Inventory by stock group",
            subledger,
            glTotal,
            contributors,
            "Stock group valuation contributors from Sage inventory valuation SQL.",
            root.TryGetProperty("matches", out var m) && m.ValueKind == JsonValueKind.True,
            new { asOfDate = asOf.ToString("yyyy-MM-dd") });
    }
}
