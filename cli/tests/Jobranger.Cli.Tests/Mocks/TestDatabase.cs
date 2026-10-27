using System.Runtime.Versioning;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Tests.Mocks;

[UnsupportedOSPlatform("windows")]
internal sealed class TestDatabase : IDisposable {
    private const string fileName = "jobranger.db";
    private const char columnSeparator = '|';
    private const string nullText = "NULL";

    private readonly TempDir temp = new();

    public TestDatabase() {
        Path = temp.File(fileName);
        Connection = Database.Open(Path, NullLogger.Instance);
    }

    public string Path { get; }

    public SqliteConnection Connection { get; }

    public static string PythonDatabase() =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", "Db", "python-v2.db");

    public static SqliteConnection OpenRaw(string path) {
        SqliteConnection connection = new(new SqliteConnectionStringBuilder {
            DataSource = path,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    public static void Execute(SqliteConnection connection, string sql, params object?[] values) {
        using SqliteCommand command = Command(connection, sql, values);
        command.ExecuteNonQuery();
    }

    public static List<string> Rows(SqliteConnection connection, string sql) {
        using SqliteCommand command = Command(connection, sql, []);
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> rows = [];
        while (reader.Read()) {
            rows.Add(string.Join(columnSeparator, Enumerable.Range(0, reader.FieldCount)
                .Select(column => reader.IsDBNull(column) ? nullText : reader.GetString(column))));
        }
        return rows;
    }

    public void Execute(string sql, params object?[] values) => Execute(Connection, sql, values);

    public object? Scalar(string sql, params object?[] values) {
        using SqliteCommand command = Command(Connection, sql, values);
        object? result = command.ExecuteScalar();
        return result is DBNull ? null : result;
    }

    public void Dispose() {
        Connection.Dispose();
        temp.Dispose();
    }

    private static SqliteCommand Command(SqliteConnection connection, string sql, object?[] values) {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (object? value in values) {
            command.Parameters.AddWithValue("$" + command.Parameters.Count, value ?? DBNull.Value);
        }
        return command;
    }
}
