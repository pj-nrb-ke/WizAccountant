using Microsoft.Data.SqlClient;

namespace WizConnector.Setup;

internal static class SqlDatabaseLister
{
    public static string BuildMasterConnectionString(
        string server,
        bool useWindowsAuthentication,
        string sqlUser,
        string sqlPassword)
    {
        if (useWindowsAuthentication)
        {
            return $"Data Source={server};Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
        }

        return $"Data Source={server};Initial Catalog=master;User ID={sqlUser};Password={sqlPassword};TrustServerCertificate=True;Encrypt=False";
    }

    public static async Task<IReadOnlyList<string>> ListDatabasesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT name
            FROM sys.databases
            WHERE database_id > 4 AND state = 0
            ORDER BY name
            """;

        var names = new List<string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            names.Add(reader.GetString(0));

        return names;
    }
}
