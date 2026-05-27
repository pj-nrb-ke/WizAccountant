using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace WizConnector.Service.Sage;

/// <summary>P3: local SQLite idempotency — prevents duplicate posts.</summary>
public sealed class IdempotencyStore
{
    private readonly string _dbPath;

    public IdempotencyStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WizConnector");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "idempotency.db");
        EnsureSchema();
    }

    public bool TryGetResult(string key, out string? resultJson)
    {
        resultJson = null;
        if (string.IsNullOrWhiteSpace(key)) return false;

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ResultJson FROM IdempotencyResults WHERE IdempotencyKey = $k";
        cmd.Parameters.AddWithValue("$k", key);
        var val = cmd.ExecuteScalar();
        if (val is string s)
        {
            resultJson = s;
            return true;
        }
        return false;
    }

    public void SaveResult(string key, string resultJson)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO IdempotencyResults (IdempotencyKey, ResultHash, ResultJson, CreatedAtUtc)
            VALUES ($k, $h, $j, $t)
            ON CONFLICT(IdempotencyKey) DO UPDATE SET ResultJson = excluded.ResultJson, CreatedAtUtc = excluded.CreatedAtUtc
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$h", Hash(resultJson));
        cmd.Parameters.AddWithValue("$j", resultJson);
        cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS IdempotencyResults (
                IdempotencyKey TEXT NOT NULL PRIMARY KEY,
                ResultHash TEXT NOT NULL,
                ResultJson TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
