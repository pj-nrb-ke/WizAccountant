using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Tests for GAP-030/031: Treasury ExplainabilityEnvelope enrichment
/// and VAT variance contributors split by DocType.
/// </summary>
public sealed class TreasuryAndVatExplainabilityTests
{
    private static QueryIntentContract DefaultContract() =>
        QueryIntentContract.Parse("test", SageIntentEngine.Classify("show me"), null);

    // ── OutputContractValidator — treasury.dashboard ──────────────────────────

    [Fact]
    public void TreasuryDashboard_WithNewFields_Passes()
    {
        var json = """
            {
              "cashPosition": 50000,
              "expectedInflows": 20000,
              "expectedOutflows": 15000,
              "projectedClosingCash": 55000,
              "finding": "Cash position stable",
              "likelyCause": "Cash position appears stable",
              "topContributors": [],
              "cashDrivers": { "topArBlockers": [], "topApPressure": [] }
            }
            """;
        var r = OutputContractValidator.Validate(DefaultContract(), "treasury.dashboard", json);
        Assert.True(r.IsValid, string.Join(", ", r.MissingFields));
    }

    [Fact]
    public void TreasuryDashboard_WithoutOptionalEnrichment_StillPasses()
    {
        // cashDrivers / topContributors / likelyCause are additive — not required
        var json = """
            {
              "cashPosition": 50000,
              "expectedInflows": 20000,
              "expectedOutflows": 15000,
              "projectedClosingCash": 55000,
              "finding": "OK"
            }
            """;
        var r = OutputContractValidator.Validate(DefaultContract(), "treasury.dashboard", json);
        Assert.True(r.IsValid, string.Join(", ", r.MissingFields));
    }

    // ── OutputContractValidator — vat.variance.contributors ──────────────────

    [Fact]
    public void VatVarianceContributors_AllNewFields_Passes()
    {
        var json = """
            {
              "difference": 150.00,
              "reconciled": false,
              "finding": "3 invoices drive variance",
              "outputVatTopContributors": [],
              "inputVatTopContributors": []
            }
            """;
        var r = OutputContractValidator.Validate(DefaultContract(), "vat.variance.contributors", json);
        Assert.True(r.IsValid, string.Join(", ", r.MissingFields));
    }

    [Fact]
    public void VatVarianceContributors_MissingOutputContribs_Fails()
    {
        var json = """
            { "difference": 0, "reconciled": true, "finding": "OK", "inputVatTopContributors": [] }
            """;
        var r = OutputContractValidator.Validate(DefaultContract(), "vat.variance.contributors", json);
        Assert.False(r.IsValid);
        Assert.Contains("outputVatTopContributors", r.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void VatVarianceContributors_MissingInputContribs_Fails()
    {
        var json = """
            { "difference": 0, "reconciled": true, "finding": "OK", "outputVatTopContributors": [] }
            """;
        var r = OutputContractValidator.Validate(DefaultContract(), "vat.variance.contributors", json);
        Assert.False(r.IsValid);
        Assert.Contains("inputVatTopContributors", r.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void VatVarianceContributors_MissingFinding_Fails()
    {
        var json = """
            { "difference": 0, "reconciled": true, "outputVatTopContributors": [], "inputVatTopContributors": [] }
            """;
        var r = OutputContractValidator.Validate(DefaultContract(), "vat.variance.contributors", json);
        Assert.False(r.IsValid);
        Assert.Contains("finding", r.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void VatVarianceContributors_Null_Fails()
        => Assert.False(OutputContractValidator.Validate(DefaultContract(), "vat.variance.contributors", null).IsValid);

    // ── ExplainabilityEnvelope — treasury drilldown hint ─────────────────────

    [Fact]
    public void ExplainabilityEnvelope_TreasuryOperation_IsExplainabilityOp()
        => Assert.True(ExplainabilityEnvelope.IsExplainabilityOperation("treasury.dashboard"));

    [Fact]
    public void ExplainabilityEnvelope_TreasuryEnhance_ContainsDrilldownHint()
    {
        var json = """
            {
              "finding": "Payables pressure detected",
              "likelyCause": "AP outstanding exceeds bank balance",
              "topContributors": [{ "account": "SUPP01", "type": "outflow_pressure", "outstanding": 85000 }]
            }
            """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var enhanced = ExplainabilityEnvelope.EnhanceReply("treasury.dashboard", "base reply", doc.RootElement);
        Assert.Contains("AR", enhanced, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AP", enhanced, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplainabilityEnvelope_TreasuryEnhance_IncludesLikelyCause()
    {
        var json = """
            {
              "finding": "Cash concerns",
              "likelyCause": "Collections lagging — AR outstanding exceeds 2× bank balance"
            }
            """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var enhanced = ExplainabilityEnvelope.EnhanceReply("treasury.dashboard", "original", doc.RootElement);
        Assert.Contains("Collections lagging", enhanced);
    }

    [Fact]
    public void ExplainabilityEnvelope_TreasuryEnhance_ListsTopContributors()
    {
        var json = """
            {
              "finding": "Cash low",
              "topContributors": [
                { "account": "CUST01", "description": "Customer CUST01 — AR 50000 outstanding" },
                { "account": "SUPP01", "description": "Supplier SUPP01 — AP 30000 outstanding" }
              ]
            }
            """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var enhanced = ExplainabilityEnvelope.EnhanceReply("treasury.dashboard", "original", doc.RootElement);
        Assert.Contains("Top contributors:", enhanced);
    }

    // ── Semantic routing — treasury keyword ──────────────────────────────────

    [Theory]
    [InlineData("cash low")]
    [InlineData("why is cash")]
    public void Semantics_TreasuryKeywords_RouteToTreasuryDashboard(string phrase)
    {
        var route = BusinessProcessSemantics.Match(phrase);
        Assert.NotNull(route);
        Assert.Equal("treasury.dashboard", route!.CanonicalOperation);
    }

    // ── HandlerCapabilityRegistry — treasury ─────────────────────────────────

    [Fact]
    public void Registry_TreasuryDashboard_IsRegistered()
        => Assert.NotNull(HandlerCapabilityRegistry.Get("treasury.dashboard"));

    [Fact]
    public void Registry_VatVarianceContributors_IsRegistered()
        => Assert.NotNull(HandlerCapabilityRegistry.Get("vat.variance.contributors"));

    // ── InsightReadOnlyTools — both ops allowed ───────────────────────────────

    [Theory]
    [InlineData("treasury.dashboard")]
    [InlineData("vat.variance.contributors")]
    public void AllowList_TreasuryAndVatVariance_Permitted(string op)
        => Assert.True(InsightReadOnlyTools.IsAllowed(op));
}
