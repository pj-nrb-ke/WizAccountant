using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class InvoiceLineSqlHintTests
{
    [Fact]
    public void ParseInvoiceLineHint_reads_connector_json()
    {
        const string json = """
            {
              "tableName": "_btblInvoiceLines",
              "columns": ["fQtyChange", "fLineTotExcl"],
              "qtyColumn": "fQtyChange",
              "qtyExpression": "ABS(ISNULL(l.fQtyChange, 0))",
              "valueExpression": "ISNULL(l.fLineTotExcl, 0)",
              "valueSource": "fLineTotExcl",
              "sampleProductMonthlySql": "SELECT 1"
            }
            """;

        var hint = InsightSqlQueryService.ParseInvoiceLineHint(json);
        Assert.Equal("_btblInvoiceLines", hint.TableName);
        Assert.Equal("fQtyChange", hint.QtyColumn);
        Assert.Equal("fLineTotExcl", hint.ValueSource);
        Assert.Equal(2, hint.Columns.Count);
        Assert.Contains("SELECT 1", hint.SampleProductMonthlySql);
    }
}
