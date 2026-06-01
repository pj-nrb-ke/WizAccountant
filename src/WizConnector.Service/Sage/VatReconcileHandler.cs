using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class VatReconcileHandler
{
    public const string QuerySerial = "SAGE-VAT-RECON-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = VatSqlHelper.ParsePeriod(parameters);
        var output = VatOutputHandler.RunOutputTotal(connectionString, from, to);
        var input = VatInputHandler.RunInputTotal(connectionString, from, to);
        var netVat = output - input;

        var glSql = $"""
            SELECT ISNULL(SUM({GlSqlHelper.NetValueExpr}), 0)
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            LEFT JOIN _etblGLAccountTypes AT ON A.iAccountType = AT.idGLAccountType
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND (LOWER(ISNULL(A.Description,'')) LIKE '%vat%' OR LOWER(ISNULL(AT.cDescription,'')) LIKE '%vat%');
            """;

        var glVat = VatSqlHelper.RunScalar(connectionString, glSql, from, to);
        var variance = glVat - netVat;

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            invNumNetVat = netVat,
            glVatControlMovement = glVat,
            variance,
            reconciled = Math.Abs(variance) < 1m,
            finding = Math.Abs(variance) < 1m
                ? "VAT control GL movement aligns with InvNum VAT totals (within tolerance)."
                : $"VAT variance: InvNum net {netVat:N2} vs GL VAT accounts {glVat:N2} (diff {variance:N2}).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
