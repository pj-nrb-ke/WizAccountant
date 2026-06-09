using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Tests for GAP-014: InvestigationContext entity-code persistence across conversation turns.
/// Verifies that entity codes tagged into ToolsUsedJson are recovered by FromPriorAssistantMessage
/// and correctly applied as fallback parameters in ApplyFollowUp.
/// </summary>
public sealed class InvestigationContextPersistenceTests
{
    private static string ToolsJson(params string[] tags) =>
        JsonSerializer.Serialize(tags.ToList());

    // ── FromPriorAssistantMessage — entity code recovery ─────────────────────

    [Fact]
    public void Recovery_CustomerCode_IsExtracted()
    {
        var json = ToolsJson("intent:ar", "customer.openitems", "entity:customerCode:SMITH001");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        Assert.NotNull(ctx);
        Assert.Equal("SMITH001", ctx!.CustomerCode);
    }

    [Fact]
    public void Recovery_SupplierCode_IsExtracted()
    {
        var json = ToolsJson("intent:ap", "supplier.openitems", "entity:supplierCode:ACME01");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        Assert.NotNull(ctx);
        Assert.Equal("ACME01", ctx!.SupplierCode);
    }

    [Fact]
    public void Recovery_StockCode_IsExtracted()
    {
        var json = ToolsJson("intent:inventory", "inventory.gl.explain", "entity:stockCode:WIDGET-A");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        Assert.NotNull(ctx);
        Assert.Equal("WIDGET-A", ctx!.StockCode);
    }

    [Fact]
    public void Recovery_WarehouseCode_IsExtracted()
    {
        var json = ToolsJson("intent:inventory", "inventory.warehouse.reconcile", "entity:warehouseCode:WH02");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        Assert.NotNull(ctx);
        Assert.Equal("WH02", ctx!.WarehouseCode);
    }

    [Fact]
    public void Recovery_MultipleEntityCodes_AllExtracted()
    {
        var json = ToolsJson(
            "intent:ar", "customer.payment.detail",
            "entity:customerCode:CUST99",
            "entity:warehouseCode:WHX");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        Assert.NotNull(ctx);
        Assert.Equal("CUST99", ctx!.CustomerCode);
        Assert.Equal("WHX",    ctx!.WarehouseCode);
        Assert.Null(ctx!.SupplierCode);
        Assert.Null(ctx!.StockCode);
    }

    [Fact]
    public void Recovery_NoEntityTags_CodesAreNull()
    {
        var json = ToolsJson("intent:ar", "customer.openitems");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        Assert.NotNull(ctx);
        Assert.Null(ctx!.CustomerCode);
        Assert.Null(ctx!.SupplierCode);
        Assert.Null(ctx!.StockCode);
        Assert.Null(ctx!.WarehouseCode);
    }

    [Fact]
    public void Recovery_LastOperation_ExcludesEntityTags()
    {
        var json = ToolsJson("intent:ar", "customer.openitems", "entity:customerCode:X001");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        Assert.NotNull(ctx);
        // entity: tag must not bleed into LastOperation
        Assert.Equal("customer.openitems", ctx!.LastOperation);
    }

    [Fact]
    public void Recovery_NullToolsJson_ReturnsNull()
        => Assert.Null(InvestigationContext.FromPriorAssistantMessage(null, null));

    [Fact]
    public void Recovery_EmptyToolsJson_ReturnsNull()
        => Assert.Null(InvestigationContext.FromPriorAssistantMessage("[]", null));

    // ── ApplyFollowUp — persisted codes applied as fallback ───────────────────

    [Fact]
    public void ApplyFollowUp_PersistedCustomerCode_AppliedWhenNotInMessage()
    {
        var json = ToolsJson("customer.payment.detail", "entity:customerCode:SMITH001");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        var parameters = new Dictionary<string, string>();
        ctx!.ApplyFollowUp("show me their open invoices", parameters);
        Assert.Equal("SMITH001", parameters["customerCode"]);
    }

    [Fact]
    public void ApplyFollowUp_PersistedSupplierCode_AppliedWhenNotInMessage()
    {
        var json = ToolsJson("supplier.openitems", "entity:supplierCode:ACME01");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        var parameters = new Dictionary<string, string>();
        ctx!.ApplyFollowUp("what about their aged balance", parameters);
        Assert.Equal("ACME01", parameters["supplierCode"]);
    }

    [Fact]
    public void ApplyFollowUp_PersistedWarehouseCode_AppliedWhenNotInMessage()
    {
        var json = ToolsJson("inventory.warehouse.reconcile", "entity:warehouseCode:WH02");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        var parameters = new Dictionary<string, string>();
        ctx!.ApplyFollowUp("show me the detail for that", parameters);
        Assert.Equal("WH02", parameters["warehouseCode"]);
    }

    [Fact]
    public void ApplyFollowUp_CurrentMessageCode_OverridesPersistedCode()
    {
        // Current message explicitly mentions a DIFFERENT customer — should win
        var json = ToolsJson("customer.openitems", "entity:customerCode:OLD001");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        var parameters = new Dictionary<string, string>();
        ctx!.ApplyFollowUp("show me customer NEW002 open items", parameters);
        Assert.Equal("NEW002", parameters["customerCode"]);
    }

    [Fact]
    public void ApplyFollowUp_PersistedStockCode_AppliedOnTransactionFollowUp()
    {
        var json = ToolsJson("inventory.gl.explain", "entity:stockCode:PROD-X");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        var parameters = new Dictionary<string, string>();
        ctx!.ApplyFollowUp("show me the transactions for that item", parameters);
        Assert.Equal("PROD-X", parameters["stockCode"]);
    }

    [Fact]
    public void ApplyFollowUp_PriorOpAlwaysPropagated()
    {
        var json = ToolsJson("inventory.gl.explain", "entity:stockCode:ITM01");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        var parameters = new Dictionary<string, string>();
        ctx!.ApplyFollowUp("any follow-up question", parameters);
        Assert.Equal("inventory.gl.explain", parameters["investigationPriorOp"]);
    }

    // ── Tag format edge cases ─────────────────────────────────────────────────

    [Fact]
    public void Recovery_EntityTagWithColonInValue_HandledCorrectly()
    {
        // e.g. stockCode value contains a dash — ensure split on second colon only
        var json = ToolsJson("entity:stockCode:WGT-100");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        Assert.Equal("WGT-100", ctx!.StockCode);
    }

    [Fact]
    public void Recovery_UnknownEntityKey_IsIgnored()
    {
        var json = ToolsJson("entity:unknownKey:VALUE", "customer.openitems");
        var ctx = InvestigationContext.FromPriorAssistantMessage(json, null);
        Assert.NotNull(ctx);
        Assert.Null(ctx!.CustomerCode);
    }
}
