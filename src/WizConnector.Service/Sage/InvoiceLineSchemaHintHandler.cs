using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class InvoiceLineSchemaHintHandler
{
    public const string QuerySerial = "INSIGHT-INVOICE-LINES-HINT-001";
    public const string Operation = "insight.sql.invoice-lines-hint";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        _ = parameters;
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        var hint = InvoiceLineSqlHelper.BuildHint(conn);
        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = Operation,
            hint.TableName,
            columns = hint.Columns,
            qtyColumn = hint.QtyColumn,
            qtyExpression = hint.QtyExpression,
            valueExpression = hint.ValueExpression,
            valueSource = hint.ValueSource,
            sampleProductMonthlySql = hint.SampleProductMonthlySql,
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
