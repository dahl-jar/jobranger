using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Storage;

internal static class SqlCommands {
    public static SqliteCommand Create(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params ReadOnlySpan<(string Name, object? Value)> values) {
        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach ((string name, object? value) in values) {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        return command;
    }

    public static int Execute(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params ReadOnlySpan<(string Name, object? Value)> values) {
        using SqliteCommand command = Create(connection, transaction, sql, values);
        return command.ExecuteNonQuery();
    }

    public static long Scalar(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params ReadOnlySpan<(string Name, object? Value)> values) {
        using SqliteCommand command = Create(connection, transaction, sql, values);
        return (long)command.ExecuteScalar()!;
    }

    public static List<T> Query<T>(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        Func<SqliteDataReader, T> read,
        params ReadOnlySpan<(string Name, object? Value)> values) {
        using SqliteCommand command = Create(connection, transaction, sql, values);
        using SqliteDataReader reader = command.ExecuteReader();
        List<T> rows = [];
        while (reader.Read()) {
            rows.Add(read(reader));
        }
        return rows;
    }
}
