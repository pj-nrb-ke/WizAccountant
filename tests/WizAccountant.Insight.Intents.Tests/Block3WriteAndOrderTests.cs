using WizAccountant.Api.Insight;
using WizAccountant.Api.Act;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Phase 4 Block 3 — Write handler expansion + Order lifecycle tests.
/// Covers: ProposalTypeMap (Tasks #16 + #17), HandlerCapabilityRegistry write entries,
/// and ReadOnlyTools exclusion contract.
/// Note: ConnectorWriteAllowlist lives in WizConnector.Service (not referenced here);
/// its 15-op content is validated indirectly by contract checks below.
/// </summary>
public sealed class Block3WriteAndOrderTests
{
    // ── Known Phase 4 Block 3 write operations ───────────────────────────────

    private static readonly string[] Phase4Block3Ops =
    [
        "inventory.adjustment.post",
        "warehouse.transfer.post",
        "salescreditnote.post",
        "suppliercreditnote.post",
        "salesorder.confirm",
        "salesorder.ship",
        "purchaseorder.approve",
        "purchaseorder.receive",
    ];

    // ── ProposalTypeMap ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("gl.journal", "gltransaction.post")]
    [InlineData("ar.transaction", "customertransaction.post")]
    [InlineData("ap.transaction", "suppliertransaction.post")]
    [InlineData("ar.allocation", "allocation.save")]
    [InlineData("customer.master", "customer.save")]
    [InlineData("supplier.master", "supplier.save")]
    public void ProposalTypeMap_Phase3_MapsCorrectly(string proposalType, string expected)
    {
        Assert.Equal(expected, ProposalTypeMap.ToOperation(proposalType));
    }

    [Theory]
    [InlineData("inventory.adjustment", "inventory.adjustment.post")]
    [InlineData("warehouse.transfer", "warehouse.transfer.post")]
    [InlineData("sales.creditnote", "salescreditnote.post")]
    [InlineData("supplier.creditnote", "suppliercreditnote.post")]
    public void ProposalTypeMap_Task16_MapsCorrectly(string proposalType, string expected)
    {
        Assert.Equal(expected, ProposalTypeMap.ToOperation(proposalType));
    }

    [Theory]
    [InlineData("salesorder.confirm", "salesorder.confirm")]
    [InlineData("salesorder.ship", "salesorder.ship")]
    [InlineData("purchaseorder.approve", "purchaseorder.approve")]
    [InlineData("purchaseorder.receive", "purchaseorder.receive")]
    public void ProposalTypeMap_Task17_MapsCorrectly(string proposalType, string expected)
    {
        Assert.Equal(expected, ProposalTypeMap.ToOperation(proposalType));
    }

    [Fact]
    public void ProposalTypeMap_IsCaseInsensitive()
    {
        Assert.Equal("inventory.adjustment.post", ProposalTypeMap.ToOperation("INVENTORY.ADJUSTMENT"));
        Assert.Equal("salesorder.confirm", ProposalTypeMap.ToOperation("SalesOrder.Confirm"));
        Assert.Equal("purchaseorder.receive", ProposalTypeMap.ToOperation("PURCHASEORDER.RECEIVE"));
    }

    [Fact]
    public void ProposalTypeMap_Unknown_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProposalTypeMap.ToOperation("unknown.bogus"));
        Assert.Contains("Unknown proposal type", ex.Message);
    }

    // ── HandlerCapabilityRegistry — write entries ────────────────────────────

    [Theory]
    [InlineData("inventory.adjustment.post")]
    [InlineData("warehouse.transfer.post")]
    [InlineData("salescreditnote.post")]
    [InlineData("suppliercreditnote.post")]
    [InlineData("salesorder.confirm")]
    [InlineData("salesorder.ship")]
    [InlineData("purchaseorder.approve")]
    [InlineData("purchaseorder.receive")]
    public void Registry_Phase4WriteOps_Present(string operation)
    {
        Assert.True(HandlerCapabilityRegistry.All.ContainsKey(operation),
            $"Operation '{operation}' missing from HandlerCapabilityRegistry.");
    }

    [Fact]
    public void Registry_InventoryAdjustmentPost_EvidenceIsStkMovement()
    {
        var cap = HandlerCapabilityRegistry.All["inventory.adjustment.post"];
        Assert.Equal("StkMovement", cap.EvidenceSource);
    }

    [Fact]
    public void Registry_WarehouseTransferPost_EvidenceIsWhseStock()
    {
        var cap = HandlerCapabilityRegistry.All["warehouse.transfer.post"];
        Assert.Equal("WhseStock", cap.EvidenceSource);
    }

    [Fact]
    public void Registry_CreditNotes_EvidenceIsInvNum()
    {
        Assert.Equal("InvNum", HandlerCapabilityRegistry.All["salescreditnote.post"].EvidenceSource);
        Assert.Equal("InvNum", HandlerCapabilityRegistry.All["suppliercreditnote.post"].EvidenceSource);
    }

    [Theory]
    [InlineData("salesorder.confirm", "SalesOrder")]
    [InlineData("salesorder.ship", "SalesOrder")]
    [InlineData("purchaseorder.approve", "PurchaseOrder")]
    [InlineData("purchaseorder.receive", "PurchaseOrder")]
    public void Registry_OrderLifecycle_EvidenceCorrect(string op, string expectedEvidence)
    {
        Assert.Equal(expectedEvidence, HandlerCapabilityRegistry.All[op].EvidenceSource);
    }

    [Theory]
    [InlineData("inventory.adjustment.post")]
    [InlineData("warehouse.transfer.post")]
    [InlineData("salescreditnote.post")]
    [InlineData("suppliercreditnote.post")]
    [InlineData("salesorder.confirm")]
    [InlineData("salesorder.ship")]
    [InlineData("purchaseorder.approve")]
    [InlineData("purchaseorder.receive")]
    public void Registry_Phase4WriteOps_ShapeIsSingle(string operation)
    {
        var cap = HandlerCapabilityRegistry.All[operation];
        Assert.Contains("single", cap.SupportsOutputShapes);
    }

    // ── ReadOnlyTools exclusion contract ─────────────────────────────────────

    [Theory]
    [InlineData("inventory.adjustment.post")]
    [InlineData("warehouse.transfer.post")]
    [InlineData("salescreditnote.post")]
    [InlineData("suppliercreditnote.post")]
    [InlineData("salesorder.confirm")]
    [InlineData("salesorder.ship")]
    [InlineData("purchaseorder.approve")]
    [InlineData("purchaseorder.receive")]
    public void ReadOnlyTools_DoesNotContainWriteOps(string operation)
    {
        // Write ops must go through the Act pipeline, never the Insight read-only path
        Assert.DoesNotContain(operation, InsightReadOnlyTools.Allowed);
    }

    [Fact]
    public void ReadOnlyTools_DoesNotContainAnyPhase3WriteOps()
    {
        var phase3Write = new[]
        {
            "gltransaction.post", "customertransaction.post", "suppliertransaction.post",
            "allocation.save", "customer.save", "supplier.save", "salesorder.save"
        };
        var overlap = phase3Write.Intersect(InsightReadOnlyTools.Allowed).ToList();
        Assert.Empty(overlap);
    }

    [Fact]
    public void ReadOnlyTools_NoWriteOpsAtAll()
    {
        // No operation ending in .post, .save, .confirm, .ship, .approve, .receive
        // should be in the read-only allowlist
        var writePatterns = new[] { ".post", ".save", ".confirm", ".ship", ".approve", ".receive" };
        var violators = InsightReadOnlyTools.Allowed
            .Where(op => writePatterns.Any(p => op.EndsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.Empty(violators);
    }

    // ── Registry completeness for all 8 new ops ───────────────────────────────

    [Fact]
    public void Registry_AllPhase4Block3OpsPresent()
    {
        var missing = Phase4Block3Ops.Where(op => !HandlerCapabilityRegistry.All.ContainsKey(op)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Registry_AllPhase4Block3Ops_NotInReadOnlyAllowlist()
    {
        var overlap = Phase4Block3Ops.Intersect(InsightReadOnlyTools.Allowed).ToList();
        Assert.Empty(overlap);
    }

    [Fact]
    public void ProposalTypeMap_AllPhase4Block3Ops_Reachable()
    {
        // Every Phase4 Block3 write operation should be reachable via ProposalTypeMap
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ProposalTypeMap.ToOperation("inventory.adjustment"),
            ProposalTypeMap.ToOperation("warehouse.transfer"),
            ProposalTypeMap.ToOperation("sales.creditnote"),
            ProposalTypeMap.ToOperation("supplier.creditnote"),
            ProposalTypeMap.ToOperation("salesorder.confirm"),
            ProposalTypeMap.ToOperation("salesorder.ship"),
            ProposalTypeMap.ToOperation("purchaseorder.approve"),
            ProposalTypeMap.ToOperation("purchaseorder.receive"),
        };

        foreach (var op in Phase4Block3Ops)
            Assert.Contains(op, reachable);
    }
}
