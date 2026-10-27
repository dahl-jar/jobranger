using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Storage;

internal static class ReaderValues {
    public static string? OptionalText(this SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
