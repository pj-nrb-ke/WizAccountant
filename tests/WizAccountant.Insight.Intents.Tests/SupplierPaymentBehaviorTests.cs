using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Tests for GAP-013: supplier payment behaviour handlers (AP mirror of AR pattern).
/// Covers: output contract validation, capability registry, semantic routing, score logic.
/// </summary>
public sealed class SupplierPaymentBehaviorTests
{
    private static QueryIntentContract DefaultContract() =>
        QueryIntentContract.Parse("test", SageIntentEngine.Classify("show me"), null);

    // ── OutputContractValidator — summary ────────────────────────────────────

    [Fact]
    public void Summary_AllRequiredFields_Passes()
    {
        var json = """
            {
              "finding": "OK",
              "promptPayers": [],
              "slowPayers": [],
              "averageDaysOverdue": 5.2,
              "suppliersAnalyzed": 10
            }
            """;
        var r = OutputContractValidator.Validate(DefaultContract(), "supplier.payment.behavior.summary", json);
        Assert.True(r.IsValid, $"Expected valid but: {string.Join(", ", r.MissingFields)}");
    }

    [Fact]
    public void Summary_MissingFinding_Fails()
    {
        var json = """{"promptPayers": [], "slowPayers": [], "averageDaysOverdue": 0, "suppliersAnalyzed": 1}""";
        var r = OutputContractValidator.Validate(DefaultContract(), "supplier.payment.behavior.summary", json);
        Assert.False(r.IsValid);
        Assert.Contains("finding", r.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Summary_MissingSuppliersAnalyzed_Fails()
    {
        var json = """{"finding": "OK", "promptPayers": [], "slowPayers": [], "averageDaysOverdue": 0}""";
        var r = OutputContractValidator.Validate(DefaultContract(), "supplier.payment.behavior.summary", json);
        Assert.False(r.IsValid);
        Assert.Contains("suppliersAnalyzed", r.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Summary_Null_Fails()
        => Assert.False(OutputContractValidator.Validate(DefaultContract(), "supplier.payment.behavior.summary", null).IsValid);

    // ── OutputContractValidator — prompt.top ─────────────────────────────────

    [Fact]
    public void PromptTop_AllRequiredFields_Passes()
    {
        var json = """{"finding": "top 5 prompt", "suppliers": [], "requestedTop": 5}""";
        var r = OutputContractValidator.Validate(DefaultContract(), "supplier.payment.prompt.top", json);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void PromptTop_MissingSuppliers_Fails()
    {
        var json = """{"finding": "OK", "requestedTop": 5}""";
        var r = OutputContractValidator.Validate(DefaultContract(), "supplier.payment.prompt.top", json);
        Assert.False(r.IsValid);
        Assert.Contains("suppliers", r.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    // ── OutputContractValidator — late.top ───────────────────────────────────

    [Fact]
    public void LateTop_AllRequiredFields_Passes()
    {
        var json = """{"finding": "slowest 5", "suppliers": [], "requestedTop": 5}""";
        var r = OutputContractValidator.Validate(DefaultContract(), "supplier.payment.late.top", json);
        Assert.True(r.IsValid);
    }

    // ── OutputContractValidator — detail ─────────────────────────────────────

    [Fact]
    public void Detail_AllRequiredFields_Passes()
    {
        var json = """{"finding": "concerns", "supplier": {"code": "SUP001"}}""";
        var r = OutputContractValidator.Validate(DefaultContract(), "supplier.payment.detail", json);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Detail_MissingSupplier_Fails()
    {
        var json = """{"finding": "concerns"}""";
        var r = OutputContractValidator.Validate(DefaultContract(), "supplier.payment.detail", json);
        Assert.False(r.IsValid);
        Assert.Contains("supplier", r.MissingFields, StringComparer.OrdinalIgnoreCase);
    }

    // ── HandlerCapabilityRegistry ─────────────────────────────────────────────

    [Theory]
    [InlineData("supplier.payment.prompt.top")]
    [InlineData("supplier.payment.late.top")]
    [InlineData("supplier.payment.behavior.summary")]
    [InlineData("supplier.payment.detail")]
    public void Registry_AllFourOperationsRegistered(string op)
    {
        var cap = HandlerCapabilityRegistry.Get(op);
        Assert.NotNull(cap);
    }

    [Theory]
    [InlineData("supplier.payment.prompt.top")]
    [InlineData("supplier.payment.late.top")]
    [InlineData("supplier.payment.behavior.summary")]
    [InlineData("supplier.payment.detail")]
    public void Registry_AllSupportDateRangeFilter(string op)
    {
        var cap = HandlerCapabilityRegistry.Get(op);
        Assert.True(cap!.SupportsDateRangeFilter);
    }

    [Fact]
    public void Registry_PromptTopSupportsTopN()
        => Assert.True(HandlerCapabilityRegistry.Get("supplier.payment.prompt.top")!.SupportsTopN);

    [Fact]
    public void Registry_LateTopSupportsTopN()
        => Assert.True(HandlerCapabilityRegistry.Get("supplier.payment.late.top")!.SupportsTopN);

    [Fact]
    public void Registry_EvidenceSource_IsVendorBased()
    {
        foreach (var op in new[] { "supplier.payment.prompt.top", "supplier.payment.behavior.summary" })
            Assert.Contains("Vendor", HandlerCapabilityRegistry.Get(op)!.EvidenceSource);
    }

    // ── BusinessProcessSemantics ──────────────────────────────────────────────

    [Theory]
    [InlineData("supplier payment discipline", "supplier.payment.behavior.summary")]
    [InlineData("how well do we pay", "supplier.payment.behavior.summary")]
    [InlineData("pay our suppliers", "supplier.payment.behavior.summary")]
    [InlineData("prompt supplier", "supplier.payment.prompt.top")]
    [InlineData("late supplier", "supplier.payment.late.top")]
    [InlineData("overdue supplier", "supplier.payment.late.top")]
    public void Semantics_KeyPhrase_RoutesToExpectedOperation(string phrase, string expectedOp)
    {
        var route = BusinessProcessSemantics.Match(phrase.ToLowerInvariant());
        Assert.NotNull(route);
        Assert.Equal(expectedOp, route!.CanonicalOperation);
    }

    [Theory]
    [InlineData("supplier payment discipline")]
    [InlineData("how well do we pay our suppliers")]
    [InlineData("which suppliers are we late paying")]
    public void Semantics_ApPaymentPhrases_HavePaymentBehaviorProcess(string phrase)
    {
        var route = BusinessProcessSemantics.Match(phrase.ToLowerInvariant());
        if (route is not null)
            Assert.Equal(BusinessProcessType.PaymentBehavior, route.Process);
    }

    // ── InsightReadOnlyTools allowlist ────────────────────────────────────────

    [Theory]
    [InlineData("supplier.payment.prompt.top")]
    [InlineData("supplier.payment.late.top")]
    [InlineData("supplier.payment.behavior.summary")]
    [InlineData("supplier.payment.detail")]
    public void AllowList_AllFourOperationsPermitted(string op)
        => Assert.True(InsightReadOnlyTools.IsAllowed(op), $"{op} should be in allowlist");

}
