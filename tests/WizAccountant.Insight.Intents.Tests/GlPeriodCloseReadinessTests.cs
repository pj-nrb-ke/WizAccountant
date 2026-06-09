using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Tests for GAP-011: gl.period.close.readiness handler wiring.
/// Covers: output contract validation, capability registry, semantic routing.
/// The connector handler itself (SQL execution) is tested via integration test
/// in the connector project — not here, which has no DB connection.
/// </summary>
public sealed class GlPeriodCloseReadinessTests
{
    private static QueryIntentContract DefaultContract() =>
        QueryIntentContract.Parse("test", SageIntentEngine.Classify("ready to close"), null);

    // ── OutputContractValidator ───────────────────────────────────────────────

    [Fact]
    public void Validator_AllRequiredFields_Passes()
    {
        var json = """
            {
              "readyToClose": true,
              "finding": "Period May 2026 is ready to close.",
              "checks": [],
              "periodLabel": "May 2026"
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "gl.period.close.readiness", json);
        Assert.True(result.IsValid, $"Expected valid but got: {string.Join(", ", result.MissingFields)}");
    }

    [Fact]
    public void Validator_MissingReadyToClose_Fails()
    {
        var json = """{"finding": "OK", "checks": [], "periodLabel": "May 2026"}""";
        var result = OutputContractValidator.Validate(DefaultContract(), "gl.period.close.readiness", json);
        Assert.False(result.IsValid);
        Assert.Contains("readyToClose", result.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_MissingChecks_Fails()
    {
        var json = """{"readyToClose": true, "finding": "OK", "periodLabel": "May 2026"}""";
        var result = OutputContractValidator.Validate(DefaultContract(), "gl.period.close.readiness", json);
        Assert.False(result.IsValid);
        Assert.Contains("checks", result.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_MissingPeriodLabel_Fails()
    {
        var json = """{"readyToClose": false, "finding": "blockers", "checks": []}""";
        var result = OutputContractValidator.Validate(DefaultContract(), "gl.period.close.readiness", json);
        Assert.False(result.IsValid);
        Assert.Contains("periodLabel", result.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_NullJson_Fails()
    {
        var result = OutputContractValidator.Validate(DefaultContract(), "gl.period.close.readiness", null);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_BlockersAndWarningsOptional_StillPasses()
    {
        // blockers/warnings are optional fields — only readyToClose, finding, checks, periodLabel required
        var json = """
            {
              "readyToClose": false,
              "finding": "2 blockers outstanding.",
              "checks": [{"checkId":"backdated_transactions","status":"FAIL","severity":"blocker","count":3,"description":"3 transactions"}],
              "periodLabel": "May 2026",
              "blockers": [{"checkId":"backdated_transactions","status":"FAIL"}],
              "warnings": []
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "gl.period.close.readiness", json);
        Assert.True(result.IsValid);
    }

    // ── HandlerCapabilityRegistry ─────────────────────────────────────────────

    [Fact]
    public void Registry_OperationIsRegistered()
    {
        var cap = HandlerCapabilityRegistry.Get("gl.period.close.readiness");
        Assert.NotNull(cap);
    }

    [Fact]
    public void Registry_SupportsDateRangeFilter()
    {
        var cap = HandlerCapabilityRegistry.Get("gl.period.close.readiness");
        Assert.True(cap!.SupportsDateRangeFilter);
    }

    [Fact]
    public void Registry_SupportsExplainability()
    {
        var cap = HandlerCapabilityRegistry.Get("gl.period.close.readiness");
        Assert.True(cap!.SupportsExplainability);
    }

    [Fact]
    public void Registry_EvidenceSourceIsCorrect()
    {
        var cap = HandlerCapabilityRegistry.Get("gl.period.close.readiness");
        Assert.Equal("PostGL+Bank", cap!.EvidenceSource);
    }

    [Fact]
    public void Registry_OutputShapesIncludeChecklist()
    {
        var cap = HandlerCapabilityRegistry.Get("gl.period.close.readiness");
        Assert.Contains("checklist", cap!.SupportsOutputShapes);
    }

    // ── BusinessProcessSemantics routing ─────────────────────────────────────

    [Theory]
    [InlineData("month-end ready")]
    [InlineData("ready to close")]
    [InlineData("close readiness")]
    [InlineData("can i close")]
    [InlineData("period close check")]
    public void Semantics_KeyPhrase_RoutesToPeriodCloseReadiness(string phrase)
    {
        var route = BusinessProcessSemantics.Match(phrase.ToLowerInvariant());
        Assert.NotNull(route);
        Assert.Equal("gl.period.close.readiness", route!.CanonicalOperation);
    }

    [Fact]
    public void Semantics_OperationProcess_IsMonthEndClose()
    {
        var route = BusinessProcessSemantics.Match("ready to close");
        Assert.NotNull(route);
        Assert.Equal(BusinessProcessType.MonthEndClose, route!.Process);
    }

    // ── BusinessProcessClassifier ─────────────────────────────────────────────

    [Theory]
    [InlineData("are we ready for period close?")]
    [InlineData("month-end close checklist")]
    [InlineData("ready to close the books")]
    public void Classifier_PeriodCloseQueries_ReturnMonthEndClose(string query)
    {
        var classification = BusinessProcessClassifier.Classify(query);
        Assert.Equal(BusinessProcessType.MonthEndClose, classification.Process);
    }
}
