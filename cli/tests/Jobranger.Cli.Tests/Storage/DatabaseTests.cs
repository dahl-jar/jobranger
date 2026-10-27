using System.Diagnostics;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Files;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Tests.Storage;

[UnsupportedOSPlatform("windows")]
public sealed class DatabaseTests : IDisposable {
    private const string schemaQuery =
        "SELECT type, name, tbl_name, sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY name";
    private const int pythonSchemaVersion = 2;

    private static readonly TimeSpan busyWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan busyWaitLimit = TimeSpan.FromSeconds(8);

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldCreatePrivateDatabase_WhenDirIsMissing() {
        string path = Path.Combine(temp.Path, "data", "jobranger", "jobranger.db");

        using SqliteConnection connection = Database.Open(path, NullLogger.Instance);

        UnixFileMode[] modes = [File.GetUnixFileMode(Path.GetDirectoryName(path)!), File.GetUnixFileMode(path)];
        Assert.Equal([FileModes.OwnerOnlyDir, FileModes.OwnerReadWrite], modes);
    }

    [Fact]
    public void ShouldRefuseOpen_WhenPathIsSymlink() {
        using TempDir outside = new();
        string target = outside.File("target.db");
        File.WriteAllText(target, "");
        string path = temp.File("jobranger.db");
        File.CreateSymbolicLink(path, target);

        PrivateFileException refused =
            Assert.Throws<PrivateFileException>(() => Database.Open(path, NullLogger.Instance));

        Assert.Equal("symbolic link", refused.Reason);
    }

    [Fact]
    public void ShouldRefuseOpen_WhenPathIsDirectory() {
        string path = temp.File("jobranger.db");
        Directory.CreateDirectory(path);

        PrivateFileException refused =
            Assert.Throws<PrivateFileException>(() => Database.Open(path, NullLogger.Instance));

        Assert.Equal("not a regular file", refused.Reason);
    }

    [Fact]
    public void ShouldTightenMode_WhenDatabaseIsOpenToOthers() {
        string path = temp.File("jobranger.db");
        Database.Open(path, NullLogger.Instance).Dispose();
        File.SetUnixFileMode(path, FileModes.OwnerReadWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        using SqliteConnection connection = Database.Open(path, NullLogger.Instance);

        Assert.Equal(FileModes.OwnerReadWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void ShouldDeleteAdActions_WhenAdIsDeleted() {
        using TestDatabase db = new();
        AdStore store = new(db.Connection, AdFixtures.Clock());
        store.Upsert(AdFixtures.Primary);
        store.MarkAd(AdFixtures.Primary.Id, AdAction.Skipped, AdFixtures.Now);

        db.Execute("DELETE FROM ads");

        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM ad_actions"));
    }

    [Fact]
    public void ShouldLeaveNativeBusyTimeoutOff_WhenOpened() {
        using TestDatabase db = new();

        Assert.Equal(0L, db.Scalar("PRAGMA busy_timeout"));
    }

    [Fact]
    public void ShouldStopWaiting_WhenDatabaseStaysLocked() {
        using TestDatabase db = new();
        using SqliteConnection holder = TestDatabase.OpenRaw(db.Path);
        using SqliteTransaction held = holder.BeginTransaction(deferred: false);
        Stopwatch watch = Stopwatch.StartNew();

        Assert.Throws<SqliteException>(() => db.Execute("INSERT INTO state (key, value) VALUES ('key', 'value')"));

        Assert.InRange(watch.Elapsed, busyWait, busyWaitLimit);
    }

    [Fact]
    public void ShouldKeepWriteLock_WhenSameProcessOpensDatabaseAgain() {
        string path = temp.File("jobranger.db");
        Database.Open(path, NullLogger.Instance).Dispose();
        using SqliteConnection holder = TestDatabase.OpenRaw(path);
        using SqliteTransaction held = holder.BeginTransaction(deferred: false);
        using SqliteConnection reopened = Database.Open(path, NullLogger.Instance);

        string refused = BeginImmediateInOtherProcess(path);

        Assert.Contains("database is locked", refused);
    }

    [Fact]
    public void ShouldCreateSameSchema_WhenComparedWithPythonDatabase() {
        using SqliteConnection python = TestDatabase.OpenRaw(TestDatabase.PythonDatabase());
        using SqliteConnection mine = TestDatabase.OpenRaw(temp.File("jobranger.db"));

        Migrations.Migrate(mine, [.. Migrations.All.Where(migration => migration.Version <= pythonSchemaVersion)]);

        Assert.Equal(TestDatabase.Rows(python, schemaQuery), TestDatabase.Rows(mine, schemaQuery));
    }

    [Fact]
    public void ShouldOpenDatabase_WhenFileIsReadOnly() {
        string path = temp.File("jobranger.db");
        Database.Open(path, NullLogger.Instance).Dispose();
        File.SetUnixFileMode(path, UnixFileMode.UserRead);

        using SqliteConnection connection = Database.Open(path, NullLogger.Instance);

        int version = Migrations.CurrentVersion(connection);
        Assert.Equal(Migrations.All[^1].Version, version);
    }

    private static string BeginImmediateInOtherProcess(string path) {
        using Process process = Process.Start(new ProcessStartInfo("sqlite3", [path, "BEGIN IMMEDIATE;"]) {
            RedirectStandardError = true,
        })!;
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return error;
    }
}
