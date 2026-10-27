using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Storage;

internal sealed class StateStore(SqliteConnection connection) : IStateStore {
    public const string SchemaVersionKey = "schema_version";

    private const string getSql = "SELECT value FROM state WHERE key = $key";
    private const string setSql =
        "INSERT INTO state (key, value) VALUES ($key, $value) "
        + "ON CONFLICT (key) DO UPDATE SET value = excluded.value";
    private const string cursorsSql = "SELECT key, value FROM state WHERE key != $key";

    public string? Get(string key) => Get(connection, null, key);

    public void Set(string key, string value) => Set(connection, null, key, value);

    public IReadOnlyDictionary<string, string> Cursors() {
        using SqliteCommand command = SqlCommands.Create(connection, null, cursorsSql, ("$key", SchemaVersionKey));
        using SqliteDataReader reader = command.ExecuteReader();
        Dictionary<string, string> cursors = new(StringComparer.Ordinal);
        while (reader.Read()) {
            cursors[reader.GetString(0)] = reader.GetString(1);
        }
        return cursors;
    }

    internal static string? Get(SqliteConnection connection, SqliteTransaction? transaction, string key) {
        using SqliteCommand command = SqlCommands.Create(connection, transaction, getSql, ("$key", key));
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? reader.GetString(0) : null;
    }

    internal static void Set(SqliteConnection connection, SqliteTransaction? transaction, string key, string value) =>
        SqlCommands.Execute(connection, transaction, setSql, ("$key", key), ("$value", value));
}
