using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-RATIOS-001 — current and quick ratio from PostGL balances.</summary>
internal static class GlFinancialRatiosHandler
{
    public const string QuerySerial = "SAGE-GL-RATIOS-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var asOf = DateTime.Today;
        var sql = $"""
            SELECT
                SUM(CASE WHEN LOWER(ISNULL(AT.cDescription,'')) LIKE '%current asset%' OR LOWER(A.Description) LIKE '%debtor%'
                    THEN {GlSqlHelper.NetValueExpr} ELSE 0 END) AS CurrentAssets,
                SUM(CASE WHEN LOWER(ISNULL(AT.cDescription,'')) LIKE '%current liab%' OR LOWER(A.Description) LIKE '%creditor%'
                    THEN -({GlSqlHelper.NetValueExpr}) ELSE 0 END) AS CurrentLiabilities,
                SUM(CASE WHEN LOWER(ISNULL(AT.cDescription,'')) LIKE '%inventory%' OR LOWER(A.Description) LIKE '%stock%'
                    THEN {GlSqlHelper.NetValueExpr} ELSE 0 END) AS Inventory
            FROM PostGL PG
            {GlSqlHelper.ExpenseJoin}
            WHERE CAST(PG.TxDate AS DATE) <= @pAsOf;
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd => cmd.Parameters.AddWithValue("@pAsOf", asOf));
        var r = rows.FirstOrDefault() ?? new Dictionary<string, object?>();
        var ca = GlSqlHelper.ToDecimal(r, "CurrentAssets");
        var cl = GlSqlHelper.ToDecimal(r, "CurrentLiabilities");
        var inv = GlSqlHelper.ToDecimal(r, "Inventory");
        var currentRatio = cl > 0 ? ca / cl : 0m;
        var quickRatio = cl > 0 ? (ca - inv) / cl : 0m;

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            asOfDate = asOf.ToString("yyyy-MM-dd"),
            currentRatio,
            quickRatio,
            currentAssets = ca,
            currentLiabilities = cl,
            inventory = inv,
            finding = $"Current ratio: {currentRatio:N2}, Quick ratio: {quickRatio:N2} (PostGL proxy by account type description).",
            note = "Financial ratios from GL balances — verify account type mapping on your chart.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
