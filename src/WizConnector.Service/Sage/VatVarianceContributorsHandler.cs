using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class VatVarianceContributorsHandler
{
    public const string QuerySerial = "SAGE-VAT-VAR-CONT-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 15);
        var (from, to) = VatSqlHelper.ParsePeriod(parameters);
        var output = VatOutputHandler.RunOutputTotal(connectionString, from, to);
        var input  = VatInputHandler.RunInputTotal(connectionString, from, to);
        var netVat = output - input;

        // Output VAT top contributors: sales invoices/credit-notes (DocType 0, 1, 4)
        const string OutputContribSql = """
            SELECT TOP (@pTop)
                H.InvNumber  AS InvoiceNumber,
                H.AccountID  AS AccountCode,
                CAST(H.InvDate AS DATE) AS InvoiceDate,
                ISNULL(H.InvTotTax, 0) AS VatAmount
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND H.DocType IN (0, 1, 4)
              AND ISNULL(H.InvTotTax, 0) <> 0
            ORDER BY ABS(ISNULL(H.InvTotTax, 0)) DESC;
            """;

        // Input VAT top contributors: purchase invoices (DocType 5)
        const string InputContribSql = """
            SELECT TOP (@pTop)
                H.InvNumber  AS InvoiceNumber,
                H.AccountID  AS AccountCode,
                CAST(H.InvDate AS DATE) AS InvoiceDate,
                ISNULL(H.InvTotTax, 0) AS VatAmount
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND H.DocType = 5
              AND ISNULL(H.InvTotTax, 0) <> 0
            ORDER BY ABS(ISNULL(H.InvTotTax, 0)) DESC;
            """;

        // Category breakdown: standard-rated (tax > 0) vs zero-rated (tax = 0)
        const string CategorySql = """
            SELECT
                SUM(CASE WHEN ISNULL(InvTotTax, 0) > 0 THEN 1 ELSE 0 END) AS StandardRated,
                SUM(CASE WHEN ISNULL(InvTotTax, 0) = 0 THEN 1 ELSE 0 END) AS ZeroRated,
                COUNT(*) AS TotalInvoices
            FROM InvNum
            WHERE CAST(InvDate AS DATE) >= @pDateFrom AND CAST(InvDate AS DATE) <= @pDateTo
              AND DocType IN (0, 1, 4, 5);
            """;

        // GL VAT control movement
        var glSql = $"""
            SELECT ISNULL(SUM({GlSqlHelper.NetValueExpr}), 0)
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            LEFT JOIN _etblGLAccountTypes AT ON A.iAccountType = AT.idGLAccountType
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND (LOWER(ISNULL(A.Description,'')) LIKE '%vat%' OR LOWER(ISNULL(AT.cDescription,'')) LIKE '%vat%');
            """;

        var outputRows = GlSqlHelper.ExecuteQuery(connectionString, OutputContribSql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            InvNumSqlHelper.AddDateParameters(cmd, from, to);
        });
        var inputRows = GlSqlHelper.ExecuteQuery(connectionString, InputContribSql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            InvNumSqlHelper.AddDateParameters(cmd, from, to);
        });
        var categoryRows = GlSqlHelper.ExecuteQuery(connectionString, CategorySql, cmd =>
            InvNumSqlHelper.AddDateParameters(cmd, from, to));
        var glVat = VatSqlHelper.RunScalar(connectionString, glSql, from, to);

        var outputContribs = outputRows.Select((r, i) => (object)new
        {
            rank = i + 1,
            invoiceNumber = r["InvoiceNumber"]?.ToString(),
            accountCode   = r["AccountCode"]?.ToString(),
            vatAmount     = GlSqlHelper.ToDecimal(r, "VatAmount")
        }).ToList();

        var inputContribs = inputRows.Select((r, i) => (object)new
        {
            rank = i + 1,
            invoiceNumber = r["InvoiceNumber"]?.ToString(),
            accountCode   = r["AccountCode"]?.ToString(),
            vatAmount     = GlSqlHelper.ToDecimal(r, "VatAmount")
        }).ToList();

        var catRow = categoryRows.FirstOrDefault();
        static int ParseInt(Dictionary<string, object>? r, string col) =>
            r is not null && r.TryGetValue(col, out var val) && int.TryParse(val?.ToString(), out var v) ? v : 0;

        var vatByCategory = new
        {
            standardRated = ParseInt(catRow, "StandardRated"),
            zeroRated     = ParseInt(catRow, "ZeroRated"),
            totalInvoices = ParseInt(catRow, "TotalInvoices")
        };

        var difference = netVat - glVat;
        var reconciled = Math.Abs(difference) < 1m;

        var payload = new Dictionary<string, object?>
        {
            ["querySerial"]              = QuerySerial,
            ["reconciliationType"]       = "VAT invoice contributors",
            ["subledgerTotal"]           = netVat,
            ["glTotal"]                  = glVat,
            ["difference"]               = difference,
            ["reconciled"]               = reconciled,
            ["matches"]                  = reconciled,
            ["finding"]                  = $"Largest VAT invoices in period {from:yyyy-MM-dd} to {to:yyyy-MM-dd}.",
            ["topContributors"]          = outputContribs.Concat(inputContribs).ToList(),
            ["outputVatTopContributors"] = outputContribs,
            ["inputVatTopContributors"]  = inputContribs,
            ["vatByCategory"]            = (object)vatByCategory,
            ["dateFrom"]                 = from.ToString("yyyy-MM-dd"),
            ["dateTo"]                   = to.ToString("yyyy-MM-dd"),
            ["dataAsOfUtc"]              = DateTimeOffset.UtcNow
        };
        return JsonSerializer.Serialize(payload);
    }
}
