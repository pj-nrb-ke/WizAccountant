using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Tests for GAP-020/021: site.schema.probe and site.metadata —
/// OutputContractValidator shapes, allowlist, and capability registry entries.
/// </summary>
public sealed class SiteSchemaAndMetadataTests
{
    private static QueryIntentContract DefaultContract() =>
        QueryIntentContract.Parse("test", SageIntentEngine.Classify("show me"), null);

    // ── site.schema.probe — OutputContractValidator ───────────────────────────

    [Fact]
    public void SchemaProbe_ValidShape_Passes()
    {
        var json = """
            {
              "tableCount": 13,
              "tablesPresent": 13,
              "tablesMissing": 0,
              "tables": [{ "tableName": "Client", "exists": true, "columnCount": 42, "columns": ["DCLink", "Name"] }],
              "missingTables": [],
              "finding": "Schema probe: 13/13 tables confirmed on this Sage database.",
              "dataAsOfUtc": "2026-06-09T10:00:00Z"
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.schema.probe", json);
        Assert.True(result.IsValid, $"Missing: {string.Join(", ", result.MissingFields)}");
    }

    [Fact]
    public void SchemaProbe_MissingTableCount_Fails()
    {
        var json = """
            {
              "tablesPresent": 10,
              "tablesMissing": 3,
              "tables": [],
              "finding": "probe"
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.schema.probe", json);
        Assert.False(result.IsValid);
        Assert.Contains("tableCount", result.MissingFields);
    }

    [Fact]
    public void SchemaProbe_MissingFinding_Fails()
    {
        var json = """
            {
              "tableCount": 5,
              "tablesPresent": 5,
              "tablesMissing": 0,
              "tables": []
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.schema.probe", json);
        Assert.False(result.IsValid);
        Assert.Contains("finding", result.MissingFields);
    }

    [Fact]
    public void SchemaProbe_EmptyJson_Fails()
    {
        var result = OutputContractValidator.Validate(DefaultContract(), "site.schema.probe", "{}");
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.MissingFields);
    }

    [Fact]
    public void SchemaProbe_NullJson_Fails()
    {
        var result = OutputContractValidator.Validate(DefaultContract(), "site.schema.probe", null);
        Assert.False(result.IsValid);
    }

    // ── site.metadata — OutputContractValidator ───────────────────────────────

    [Fact]
    public void SiteMetadata_ValidShape_Passes()
    {
        var json = """
            {
              "connectorVersion": "1.0.0.0",
              "sdkVersion": "11.0.0.0",
              "companyDatabase": "SageEvoDemo",
              "handlerCount": 115,
              "schemaProof": {
                "keyTableCount": 8,
                "confirmedTableCount": 8,
                "allKeyTablesPresent": true,
                "tables": [],
                "error": null
              },
              "capabilities": {
                "arSupported": true,
                "apSupported": true,
                "glSupported": true,
                "invoicingSupported": true,
                "inventorySupported": true
              },
              "finding": "All 8 key Sage tables confirmed — connector fully operational.",
              "dataAsOfUtc": "2026-06-09T10:00:00Z"
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.metadata", json);
        Assert.True(result.IsValid, $"Missing: {string.Join(", ", result.MissingFields)}");
    }

    [Fact]
    public void SiteMetadata_MissingConnectorVersion_Fails()
    {
        var json = """
            {
              "schemaProof": {},
              "capabilities": {},
              "finding": "ok"
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.metadata", json);
        Assert.False(result.IsValid);
        Assert.Contains("connectorVersion", result.MissingFields);
    }

    [Fact]
    public void SiteMetadata_MissingSchemaProof_Fails()
    {
        var json = """
            {
              "connectorVersion": "1.0",
              "capabilities": {},
              "finding": "ok"
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.metadata", json);
        Assert.False(result.IsValid);
        Assert.Contains("schemaProof", result.MissingFields);
    }

    [Fact]
    public void SiteMetadata_MissingCapabilities_Fails()
    {
        var json = """
            {
              "connectorVersion": "1.0",
              "schemaProof": {},
              "finding": "ok"
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.metadata", json);
        Assert.False(result.IsValid);
        Assert.Contains("capabilities", result.MissingFields);
    }

    [Fact]
    public void SiteMetadata_MissingFinding_Fails()
    {
        var json = """
            {
              "connectorVersion": "1.0",
              "schemaProof": {},
              "capabilities": {}
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.metadata", json);
        Assert.False(result.IsValid);
        Assert.Contains("finding", result.MissingFields);
    }

    // ── InsightReadOnlyTools allowlist ────────────────────────────────────────

    [Fact]
    public void AllowList_SchemaProbe_Allowed()
        => Assert.True(InsightReadOnlyTools.IsAllowed("site.schema.probe"));

    [Fact]
    public void AllowList_SiteMetadata_Allowed()
        => Assert.True(InsightReadOnlyTools.IsAllowed("site.metadata"));

    [Fact]
    public void AllowList_SchemaProbe_CaseInsensitive()
        => Assert.True(InsightReadOnlyTools.IsAllowed("SITE.SCHEMA.PROBE"));

    [Fact]
    public void AllowList_SiteMetadata_CaseInsensitive()
        => Assert.True(InsightReadOnlyTools.IsAllowed("SITE.METADATA"));

    // ── HandlerCapabilityRegistry entries ────────────────────────────────────

    [Fact]
    public void Registry_SchemaProbe_Registered()
    {
        var cap = HandlerCapabilityRegistry.Get("site.schema.probe");
        Assert.NotNull(cap);
        Assert.Equal("site.schema.probe", cap!.Operation);
    }

    [Fact]
    public void Registry_SchemaProbe_HasSchemaShape()
    {
        var cap = HandlerCapabilityRegistry.Get("site.schema.probe");
        Assert.NotNull(cap);
        Assert.Contains("schema", cap!.SupportsOutputShapes);
    }

    [Fact]
    public void Registry_SchemaProbe_EvidenceIsInformationSchema()
    {
        var cap = HandlerCapabilityRegistry.Get("site.schema.probe");
        Assert.NotNull(cap);
        Assert.Contains("INFORMATION_SCHEMA", cap!.EvidenceSource);
    }

    [Fact]
    public void Registry_SiteMetadata_Registered()
    {
        var cap = HandlerCapabilityRegistry.Get("site.metadata");
        Assert.NotNull(cap);
        Assert.Equal("site.metadata", cap!.Operation);
    }

    [Fact]
    public void Registry_SiteMetadata_HasMetadataShape()
    {
        var cap = HandlerCapabilityRegistry.Get("site.metadata");
        Assert.NotNull(cap);
        Assert.Contains("metadata", cap!.SupportsOutputShapes);
    }

    [Fact]
    public void Registry_SiteMetadata_EvidenceIncludesConnector()
    {
        var cap = HandlerCapabilityRegistry.Get("site.metadata");
        Assert.NotNull(cap);
        Assert.Contains("connector", cap!.EvidenceSource);
    }

    // ── Schema probe partial-presence scenario ────────────────────────────────

    [Fact]
    public void SchemaProbe_PartialPresence_ShapeStillValid()
    {
        // Missing tables scenario — shape is still valid even if some tables are absent
        var json = """
            {
              "tableCount": 13,
              "tablesPresent": 9,
              "tablesMissing": 4,
              "tables": [
                { "tableName": "Client", "exists": true, "columnCount": 40, "columns": ["DCLink"] },
                { "tableName": "StkMovement", "exists": false, "columnCount": 0, "columns": [] }
              ],
              "missingTables": ["StkMovement", "_etblGLAccountTypes", "GrpTbl", "_btblInvoiceLines"],
              "finding": "Schema probe: 9/13 tables confirmed. Missing: StkMovement, _etblGLAccountTypes, GrpTbl, _btblInvoiceLines.",
              "dataAsOfUtc": "2026-06-09T10:00:00Z"
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.schema.probe", json);
        Assert.True(result.IsValid, $"Missing: {string.Join(", ", result.MissingFields)}");
    }

    // ── Site metadata with schema error ──────────────────────────────────────

    [Fact]
    public void SiteMetadata_WithSchemaError_ShapeStillValid()
    {
        var json = """
            {
              "connectorVersion": "1.0.0.0",
              "schemaProof": {
                "keyTableCount": 8,
                "confirmedTableCount": 0,
                "allKeyTablesPresent": false,
                "tables": [],
                "error": "Login failed for user 'sa'."
              },
              "capabilities": {
                "arSupported": false,
                "apSupported": false,
                "glSupported": false,
                "invoicingSupported": false,
                "inventorySupported": false
              },
              "finding": "0/8 key tables present — some capabilities may be limited.",
              "dataAsOfUtc": "2026-06-09T10:00:00Z"
            }
            """;
        var result = OutputContractValidator.Validate(DefaultContract(), "site.metadata", json);
        Assert.True(result.IsValid, $"Missing: {string.Join(", ", result.MissingFields)}");
    }
}
