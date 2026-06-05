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

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var tran = conn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            string? prefix;
            long nextNumber;
            using (var selectCmd = new SqlCommand(
                       """
                       SELECT OrderPrefix, NextCustNo
                       FROM ORDERSDF WITH (UPDLOCK, HOLDLOCK)
                       WHERE iModule = @module
                       """,
                       conn,
                       tran)
                   { CommandTimeout = 60 })
            {
                selectCmd.Parameters.AddWithValue("@module", module);
                using var reader = selectCmd.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException($"ORDERSDF row not found for iModule = {module}.");

                prefix = ReadString(reader, "OrderPrefix") ?? "";
                nextNumber = ReadInt64(reader, "NextCustNo");
            }

            using (var updateCmd = new SqlCommand(
                       """
                       UPDATE ORDERSDF
                       SET NextCustNo = NextCustNo + 1
                       WHERE iModule = @module
                       """,
                       conn,
                       tran)
                   { CommandTimeout = 60 })
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
