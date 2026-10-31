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

    public static ReadFailure? Of(Exception error, Paths paths, IMessages messages) => error switch {
        ConfigMissingException => new ReadFailure(configMissing, [messages.ConfigMissing]),
        ConfigInvalidException invalid =>
            new ReadFailure(configInvalid, [.. invalid.Errors.Select(problem => problem.Describe(messages))]),
        SchemaTooNewException => new ReadFailure(schemaTooNew, [messages.DatabaseTooNew]),
        SqliteException => new ReadFailure(databaseUnreadable, [messages.DatabaseUnreadable(paths.DbPath())]),
        PrivateFileException privateFile => new ReadFailure(fileUnreadable, [messages.FileUnreadable(privateFile.Path)]),
        IOException or UnauthorizedAccessException => new ReadFailure(fileUnreadable, [messages.FileAccessFailed]),
        _ => null,
    };
}
