using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SQL fallbacks for sales-order dropdown lookups when SDK has no static List().</summary>
internal static class SalesOrderLookupSql
{
    internal static string List(
        string? companyConnectionString,
        Dictionary<string, string> parameters,
        params string[] sqlCandidates)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        Exception? last = null;
        foreach (var sql in sqlCandidates)
        {
            try
            {
                var items = Query(companyConnectionString, sql);
                return SageListHelpers.SerializePaged(items, "1=1", parameters);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException("Lookup query failed.");
    }

    internal static string TryList(
        string? companyConnectionString,
        Dictionary<string, string> parameters,
        params string[] sqlCandidates)
    {
        try
        {
            return List(companyConnectionString, parameters, sqlCandidates);
        }
        catch
        {
            return SageListHelpers.SerializePaged(new List<object>(), "1=1", parameters);
        }
    }

    private static List<object> Query(string connectionString, string sql)
    {
        var items = new List<object>();
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var code = ReadString(reader, "Code", "cCode", "idCode");
            if (string.IsNullOrWhiteSpace(code)) continue;
            items.Add(new
            {
                code,
                name = ReadString(reader, "Name", "cName", "Description", "cDescription"),
                description = ReadString(reader, "Description", "cDescription", "Name", "cName"),
                rate = ReadString(reader, "TaxRate", "fTaxRate", "Rate"),
                symbol = ReadString(reader, "Symbol", "cCurrencySymbol", "CurrencySymbol")
            });
        }

        return items;
    }

    private static string? ReadString(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                if (reader.IsDBNull(ord)) continue;
                return Convert.ToString(reader.GetValue(ord));
            }
            catch (IndexOutOfRangeException)
            {
                // try next column alias
            }
        }

        return null;
    }
}
