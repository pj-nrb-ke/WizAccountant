namespace WizAccountant.Insight.Intents.Tests;

/// <summary>SAGE-DOCS-001 P0 — InvNum DocType/DocState filter regression guards.</summary>
public class InvNumDocumentFilterTests
{
    [Fact]
    public void InvNumSqlHelper_excludes_quote_template_and_cancelled_doc_states()
    {
        var source = ReadSource("InvNumSqlHelper.cs");
        Assert.Contains("NOT IN (2, 5, 6, 7)", source);
        Assert.DoesNotContain("<> 3", source);
    }

    [Fact]
    public void Purchase_filter_excludes_ar_credit_note_doc_type_1()
    {
        var source = ReadSource("InvNumSqlHelper.cs");
        Assert.Contains("H.DocType = 5", source);
        Assert.DoesNotContain("DocType IN (1, 5)", source);
        Assert.DoesNotContain("DocType = 1 OR", source);
    }

    [Fact]
    public void Sales_filter_uses_invoice_doc_type_and_sales_order_with_doc_flag()
    {
        var source = ReadSource("InvNumSqlHelper.cs");
        Assert.Contains("H.DocType = 0", source);
        Assert.Contains("H.DocType = 4", source);
        Assert.Contains("DocFlag", source);
        Assert.Contains("IN (0, 2)", source);
    }

    [Fact]
    public void Sales_credit_note_count_uses_correct_doc_state_filter()
    {
        var source = ReadSource("SalesCreditNoteCountHandler.cs");
        Assert.Contains("InvNumSqlHelper.DocStateAnalyticsExclusionFilter", source);
        Assert.DoesNotContain("<> 3", source);
        Assert.Contains("SalesCreditNoteDocTypeFilter", source);
    }

    [Fact]
    public void Schema_hint_operation_is_registered()
    {
        var source = ReadSource("SageSdkPhase2Handlers.cs");
        Assert.Contains("\"insight.sql.invnum-documents-hint\"", source);
        Assert.Contains("InvNumDocumentSchemaHintHandler", source);
    }

    [Fact]
    public void Clarification_response_v2_is_in_docs()
    {
        var path = FindRepoFile("DOCS", "SAGE_DOCS_001_Clarification_Response_V2.md");
        var text = File.ReadAllText(path);
        Assert.Contains("Do not assume customer debit notes are `DocType = 2`", text);
        Assert.Contains("Remove DocType 1", text);
    }

    private static string ReadSource(string fileName)
    {
        var path = FindRepoFile("src", "WizConnector.Service", "Sage", fileName);
        return File.ReadAllText(path);
    }

    private static string FindRepoFile(params string[] parts)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = start;
            for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++, dir = Path.GetDirectoryName(dir))
            {
                var candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }
}
