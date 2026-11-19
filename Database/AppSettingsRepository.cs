using Dapper;
using Microsoft.Data.Sqlite;

namespace RollShopManager.Database;

/// <summary>Truy xuất bảng AppSettings (Key / Value).</summary>
public sealed class AppSettingsRepository : RepositoryBase
{
    public string? Get(string key, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ValidateKey(key);
        return WithConnection(connection, transaction, (c, t) =>
            c.QuerySingleOrDefault<string?>(
                "SELECT Value FROM AppSettings WHERE Key = @Key;",
                new { Key = key }, t));
    }

    public IReadOnlyDictionary<string, string?> GetAll(SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
        {
            Dictionary<string, string?> result = new(StringComparer.Ordinal);
            foreach (KeyValueRow row in c.Query<KeyValueRow>("SELECT Key, Value FROM AppSettings ORDER BY Key;", transaction: t))
            {
                result[row.Key] = row.Value;
            }

            return (IReadOnlyDictionary<string, string?>)result;
        });
    }

    public int Set(string key, string? value, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ValidateKey(key);
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute(
                @"INSERT INTO AppSettings (Key, Value) VALUES (@Key, @Value)
                  ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;",
                new { Key = key, Value = value }, t));
    }

    public int Delete(string key, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ValidateKey(key);
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute("DELETE FROM AppSettings WHERE Key = @Key;", new { Key = key }, t));
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Khóa cấu hình không được để trống.", nameof(key));
        }
    }

    private sealed class KeyValueRow
    {
        public string Key { get; set; } = string.Empty;

        public string? Value { get; set; }
    }
}
