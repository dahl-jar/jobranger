using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Commands;

internal static class ReadFailures {
    private const string configMissing = "config_missing";
    private const string configInvalid = "config_invalid";
    private const string schemaTooNew = "schema_too_new";
    private const string databaseUnreadable = "database_unreadable";
    private const string fileUnreadable = "file_unreadable";

    public static bool Covers(Exception error) =>
        error is ConfigMissingException or ConfigInvalidException or SchemaTooNewException or SqliteException
            or PrivateFileException;

    public static string Code(Exception error) => error switch {
        ConfigMissingException => configMissing,
        ConfigInvalidException => configInvalid,
        SchemaTooNewException => schemaTooNew,
        SqliteException => databaseUnreadable,
        PrivateFileException => fileUnreadable,
        _ => throw new ArgumentException($"read failure has no code error={error.GetType().Name}", nameof(error)),
    };

    public static IReadOnlyList<string> Lines(Exception error, Paths paths, IMessages messages) => error switch {
        ConfigMissingException => [messages.ConfigMissing],
        ConfigInvalidException invalid => [.. invalid.Errors.Select(problem => problem.Describe(messages))],
        SchemaTooNewException => [messages.DatabaseTooNew],
        SqliteException => [messages.DatabaseUnreadable(paths.DbPath())],
        PrivateFileException privateFile => [messages.FileUnreadable(privateFile.Path)],
        _ => throw new ArgumentException($"read failure has no text error={error.GetType().Name}", nameof(error)),
    };
}
