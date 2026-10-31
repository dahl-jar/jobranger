using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Commands;

internal static class ReadFailures {
    private const string configMissing = "config_missing";
    private const string profileMissing = "profile_missing";
    private const string configInvalid = "config_invalid";
    private const string schemaTooNew = "schema_too_new";
    private const string databaseUnreadable = "database_unreadable";
    private const string fileUnreadable = "file_unreadable";
    private const string outputFailed = "output_failed";

    public static int PrintLines(
        Func<IReadOnlyList<string>> read,
        Action<string> logFailure,
        Paths paths,
        IMessages messages,
        TextWriter output) {
        IReadOnlyList<string> lines;
        int exit = CliExitCodes.Ok;
        try {
            lines = read();
        } catch (Exception error) when (Of(error, paths, messages) is ReadFailure failure) {
            logFailure(failure.Code);
            lines = failure.Lines;
            exit = CliExitCodes.Failure;
        }
        try {
            foreach (string line in lines) {
                output.WriteLine(line);
            }
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            logFailure(outputFailed);
            return CliExitCodes.Failure;
        }
        return exit;
    }

    private static ReadFailure? Of(Exception error, Paths paths, IMessages messages) => error switch {
        ConfigMissingException missing when missing.Path == paths.ConfigPath() =>
            new ReadFailure(configMissing, [messages.ConfigMissing]),
        ConfigMissingException missing => new ReadFailure(profileMissing, [messages.ProfileMissing(missing.Path)]),
        ConfigInvalidException invalid =>
            new ReadFailure(configInvalid, [.. invalid.Errors.Select(problem => problem.Describe(messages))]),
        SchemaTooNewException => new ReadFailure(schemaTooNew, [messages.DatabaseTooNew]),
        SqliteException => new ReadFailure(databaseUnreadable, [messages.DatabaseUnreadable(paths.DbPath())]),
        PrivateFileException privateFile => new ReadFailure(fileUnreadable, [messages.FileUnreadable(privateFile.Path)]),
        IOException or UnauthorizedAccessException => new ReadFailure(fileUnreadable, [messages.FileAccessFailed]),
        _ => null,
    };
}
