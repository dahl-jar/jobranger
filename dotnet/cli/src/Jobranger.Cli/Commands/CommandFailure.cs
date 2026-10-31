using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Commands;

internal static class CommandFailure {
    private const string configMissing = "config_missing";
    private const string profileMissing = "profile_missing";
    private const string configInvalid = "config_invalid";
    private const string schemaTooNew = "schema_too_new";
    private const string migrationFailed = "migration_failed";
    private const string databaseUnreadable = "database_unreadable";
    private const string secretStoreFailed = "secret_store_failed";
    private const string dirNotWritable = "dir_not_writable";
    private const string fileUnreadable = "file_unreadable";

    public static ReadFailure? Of(Exception error, Paths paths, IMessages messages) => error switch {
        ConfigMissingException missing when missing.Path == paths.ConfigPath() =>
            new ReadFailure(configMissing, [messages.ConfigMissing]),
        ConfigMissingException missing => new ReadFailure(profileMissing, [messages.ProfileMissing(missing.Path)]),
        ConfigInvalidException invalid =>
            new ReadFailure(configInvalid, [.. invalid.Errors.Select(problem => problem.Describe(messages))]),
        SchemaTooNewException => new ReadFailure(schemaTooNew, [messages.DatabaseTooNew]),
        MigrationException => new ReadFailure(migrationFailed, [messages.DatabaseUnreadable(paths.DbPath())]),
        SqliteException => new ReadFailure(databaseUnreadable, [messages.DatabaseUnreadable(paths.DbPath())]),
        SecretStoreUnavailableException => new ReadFailure(secretStoreFailed, [messages.SecretStoreFailed]),
        PrivateDirException dir => new ReadFailure(dirNotWritable, [messages.DirNotWritable(dir.Path)]),
        PrivateFileException file when file.Path == paths.DbPath() =>
            new ReadFailure(databaseUnreadable, [messages.DatabaseUnreadable(paths.DbPath())]),
        PrivateFileException file => new ReadFailure(fileUnreadable, [messages.FileUnreadable(file.Path)]),
        IOException or UnauthorizedAccessException => new ReadFailure(fileUnreadable, [messages.FileAccessFailed]),
        _ => null,
    };
}
