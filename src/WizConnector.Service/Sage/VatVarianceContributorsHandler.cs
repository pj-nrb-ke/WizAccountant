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
        var input = VatInputHandler.RunInputTotal(connectionString, from, to);
        var netVat = output - input;

        var sql = $"""
            SELECT TOP (@pTop)
                H.InvNumber AS InvoiceNumber,
                H.AccountID AS AccountCode,
                CAST(H.InvDate AS DATE) AS InvoiceDate,
                ISNULL(H.InvTotTax, 0) AS VatAmount,
                ABS(ISNULL(H.InvTotTax, 0)) AS AbsVat
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND ISNULL(H.InvTotTax, 0) <> 0
            ORDER BY ABS(ISNULL(H.InvTotTax, 0)) DESC;
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            InvNumSqlHelper.AddDateParameters(cmd, from, to);
        });

        var glSql = $"""
            SELECT ISNULL(SUM({GlSqlHelper.NetValueExpr}), 0)
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            LEFT JOIN _etblGLAccountTypes AT ON A.iAccountType = AT.idGLAccountType
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND (LOWER(ISNULL(A.Description,'')) LIKE '%vat%' OR LOWER(ISNULL(AT.cDescription,'')) LIKE '%vat%');
            """;
        var glVat = VatSqlHelper.RunScalar(connectionString, glSql, from, to);

        var topList = rows.Select((r, i) => (object)new
        {
            rank = i + 1,
            invoiceNumber = r["InvoiceNumber"]?.ToString(),
            accountCode = r["AccountCode"]?.ToString(),
            vatAmount = GlSqlHelper.ToDecimal(r, "VatAmount")
        }).ToList();

        return ReconcileEnvelope.Build(
            QuerySerial,
            "VAT invoice contributors",
            netVat,
            glVat,
            topList,
            $"Largest VAT invoices in period {from:yyyy-MM-dd} to {to:yyyy-MM-dd}.",
            Math.Abs(netVat - glVat) < 1m,
            new { dateFrom = from.ToString("yyyy-MM-dd"), dateTo = to.ToString("yyyy-MM-dd") });
    }
}
