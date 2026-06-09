using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>Allocates the next sales order number from ORDERSDF with row locking.</summary>
internal static class SalesOrderNumberHandler
{
    /// <summary>Sales orders use iModule = 0 in ORDERSDF.</summary>
    public const int DefaultSalesOrderModule = 0;

    private static readonly string[] SelectSqlCandidates =
    [
        """
        SELECT OrderPrefix, NextCustNo
        FROM ORDERSDF WITH (UPDLOCK, HOLDLOCK)
        WHERE iModule = @module
        """,
        """
        SELECT cOrderPrefix AS OrderPrefix, NextCustNo
        FROM ORDERSDF WITH (UPDLOCK, HOLDLOCK)
        WHERE iModule = @module
        """,
        """
        SELECT OrderPrefix, NextCustNo
        FROM ORDERSDF WITH (UPDLOCK, HOLDLOCK)
        WHERE iModuleID = @module
        """,
        """
        SELECT TOP 1 OrderPrefix, NextCustNo
        FROM ORDERSDF WITH (UPDLOCK, HOLDLOCK)
        ORDER BY iModule
        """
    ];

    private static readonly string[] UpdateSqlCandidates =
    [
        """
        UPDATE ORDERSDF
        SET NextCustNo = NextCustNo + 1
        WHERE iModule = @module
        """,
        """
        UPDATE ORDERSDF
        SET NextCustNo = NextCustNo + 1
        WHERE iModuleID = @module
        """
    ];

    public static string AllocateNext(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var module = DefaultSalesOrderModule;
        if (parameters.TryGetValue("module", out var moduleText) &&
            int.TryParse(moduleText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedModule))
        {
            module = parsedModule;
        }

        Exception? last = null;
        foreach (var selectSql in SelectSqlCandidates)
        {
            foreach (var updateSql in UpdateSqlCandidates)
            {
                try
                {
                    return AllocateWithSql(companyConnectionString, module, selectSql, updateSql);
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }
        }

        throw last ?? new InvalidOperationException("Could not allocate sales order number from ORDERSDF.");
    }

    private static string AllocateWithSql(
        string companyConnectionString,
        int module,
        string selectSql,
        string updateSql)
    {
        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var tran = conn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            string? prefix;
            long nextNumber;
            using (var selectCmd = new SqlCommand(selectSql, conn, tran) { CommandTimeout = 60 })
            {
                if (selectSql.Contains("@module", StringComparison.Ordinal))
                    selectCmd.Parameters.AddWithValue("@module", module);

                using var reader = selectCmd.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException($"ORDERSDF row not found for iModule = {module}.");

                prefix = ReadString(reader, "OrderPrefix", "cOrderPrefix", "Prefix") ?? "";
                nextNumber = ReadInt64(reader, "NextCustNo", "iNextCustNo", "NextNumber");
                if (nextNumber <= 0)
                    throw new InvalidOperationException("ORDERSDF NextCustNo is not configured.");
            }

            using (var updateCmd = new SqlCommand(updateSql, conn, tran) { CommandTimeout = 60 })
            {
                updateCmd.Parameters.AddWithValue("@module", module);
                if (updateCmd.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException($"ORDERSDF update failed for iModule = {module}.");
            }

            tran.Commit();

            var orderNumber = $"{prefix}{nextNumber}";
            return JsonSerializer.Serialize(new
            {
                ok = true,
                orderNumber,
                orderPrefix = prefix,
                nextNumber,
                iModule = module,
                note = "NextCustNo incremented under UPDLOCK/HOLDLOCK."
            });
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    private static string? ReadString(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                if (reader.IsDBNull(ord)) continue;
                return Convert.ToString(reader.GetValue(ord))?.Trim();
            }
            catch (IndexOutOfRangeException) { }
        }

        return null;
    }

    private static long ReadInt64(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                if (reader.IsDBNull(ord)) continue;
                return Convert.ToInt64(reader.GetValue(ord), CultureInfo.InvariantCulture);
            }
            catch (IndexOutOfRangeException) { }
        }

        return 0;
    }
}
