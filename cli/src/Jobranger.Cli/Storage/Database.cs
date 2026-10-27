using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Jobranger.Cli.Storage;

internal static partial class Database {
    private const int busyTimeoutSeconds = 5;
    private const string symlinkReason = "symbolic link";
    private const string notRegularReason = "not a regular file";

    private static readonly string busyTimeoutSql = string.Create(CultureInfo.InvariantCulture,
        $"PRAGMA busy_timeout = {TimeSpan.FromSeconds(busyTimeoutSeconds).TotalMilliseconds}");

    private static readonly Lock createLock = new();

    [UnsupportedOSPlatform("windows")]
    public static SqliteConnection Open(string path, ILogger logger) {
        PrivateFile.CreateParentDir(path, logger);
        CreatePrivate(path, logger);
        SqliteConnection connection = new(new SqliteConnectionStringBuilder {
            DataSource = path,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = busyTimeoutSeconds,
        }.ToString());
        try {
            connection.Open();
            SqlCommands.Execute(connection, null, busyTimeoutSql);
            Migrations.Migrate(connection, Migrations.All);
            return connection;
        } catch {
            connection.Dispose();
            throw;
        }
    }

    public static SqliteTransaction BeginImmediate(SqliteConnection connection) =>
        connection.BeginTransaction(deferred: false);

    [UnsupportedOSPlatform("windows")]
    private static void CreatePrivate(string path, ILogger logger) {
        lock (createLock) {
            if (UnixFiles.TryCreateNew(path, PrivateFile.OwnerOnlyFileMode, out int error)) {
                return;
            }
            if (!UnixFiles.IsExisting(error)) {
                throw PrivateFile.OpenError(path, error);
            }
            CheckExisting(path, logger);
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void CheckExisting(string path, ILogger logger) {
        if (new FileInfo(path).LinkTarget is not null) {
            throw new PrivateFileException(path, symlinkReason);
        }
        if (!UnixFiles.IsRegularPath(path)) {
            throw new PrivateFileException(path, notRegularReason);
        }
        if ((File.GetUnixFileMode(path) & ~PrivateFile.OwnerOnlyFileMode) != 0) {
            File.SetUnixFileMode(path, PrivateFile.OwnerOnlyFileMode);
            LogFileTightened(logger, Path.GetFileName(path));
        }
    }

    [LoggerMessage(
        EventName = "paths.private_file.tightened",
        Level = LogLevel.Warning,
        Message = "tightened file mode to 0600 file={File}")]
    private static partial void LogFileTightened(ILogger logger, string file);
}
