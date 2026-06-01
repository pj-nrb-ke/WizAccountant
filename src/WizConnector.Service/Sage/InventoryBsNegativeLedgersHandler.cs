using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>
/// Inventory/stock GL accounts on Balance Sheet with credit (negative) net balance.
/// DOCS/Sage_AI_Negative_Stock_Balance_Sheet_Patch.md — SAGE-BS-STOCK-NEGATIVE-001.
/// </summary>
internal static class InventoryBsNegativeLedgersHandler
{
    public const string QuerySerial = "SAGE-BS-STOCK-NEGATIVE-001";

    private const string Sql = """
        DECLARE @AsOfDate DATE = @pAsOfDate;

        WITH InventoryAccounts AS
        (
            SELECT DISTINCT G.StockAccLink AS AccountLink
            FROM GrpTbl G
            WHERE ISNULL(G.StockAccLink, 0) <> 0
        ),
        GLBalances AS
        (
            SELECT
                A.AccountLink,
                A.Account,
                A.Description AS GLAccountName,
                SUM(ISNULL(PG.Debit, 0) - ISNULL(PG.Credit, 0)) AS NetBalance
            FROM InventoryAccounts IA
            INNER JOIN Accounts A ON IA.AccountLink = A.AccountLink
            LEFT JOIN PostGL PG ON PG.AccountLink = A.AccountLink
                AND CAST(PG.TxDate AS DATE) <= @AsOfDate
            GROUP BY A.AccountLink, A.Account, A.Description
        )
        SELECT
            AccountLink,
            Account AS GLAccount,
            GLAccountName,
            NetBalance
        FROM GLBalances
        WHERE NetBalance < 0
        ORDER BY NetBalance ASC;
        """;

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var asOfDate = ParseAsOfDate(parameters);
        var ledgers = new List<LedgerRow>();

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(Sql, conn) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@pAsOfDate", asOfDate);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            ledgers.Add(new LedgerRow(
                reader["GLAccount"]?.ToString() ?? "",
                reader["GLAccountName"]?.ToString() ?? "",
                ReadDecimal(reader, "NetBalance")));
        }

        var totalNegative = ledgers.Sum(l => l.NetBalance);
        var hasNegative = ledgers.Count > 0;
        var largest = ledgers.FirstOrDefault();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            intent = "inventory_balance_sheet_negative_ledgers",
            asOfDate = asOfDate.ToString("yyyy-MM-dd"),
            hasNegativeLedgers = hasNegative,
            totalNegativeStockValue = totalNegative,
            ledgerCount = ledgers.Count,
            ledgers = ledgers.Select(l => new
            {
                glAccount = CleanAccountCode(l.Account),
                glAccountName = l.AccountName,
                netBalance = l.NetBalance
            }),
            largestNegative = largest is null ? null : new
            {
                glAccount = CleanAccountCode(largest.Account),
                glAccountName = largest.AccountName,
                netBalance = largest.NetBalance
            },
            note = "Balance Sheet stock value = PostGL net balance on distinct GrpTbl.StockAccLink accounts. Negative = credit balance.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static DateTime ParseAsOfDate(Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("asOfDate", out var raw) && DateTime.TryParse(raw, out var dt))
            return dt.Date;
        return DateTime.Today;
    }

    private static decimal ReadDecimal(SqlDataReader reader, string column)
    {
        var val = reader[column];
        return val is decimal d ? d : Convert.ToDecimal(val);
    }

    private static string CleanAccountCode(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return "";
        return account.Trim().TrimStart('>');
    }

    private sealed record LedgerRow(string Account, string AccountName, decimal NetBalance);
}
