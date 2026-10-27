using System.Runtime.Versioning;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Tests.Storage;

[UnsupportedOSPlatform("windows")]
public sealed class MigrationsTests : IDisposable {
    private const int openers = 3;

    private static readonly Migration first = new(1, ["CREATE TABLE state (key TEXT PRIMARY KEY, value TEXT)"]);
    private static readonly Migration second = new(2, ["CREATE TABLE extra (id INTEGER PRIMARY KEY)"]);

    private readonly TempDir temp = new();
    private readonly SqliteConnection connection;

    public MigrationsTests() {
        connection = TestDatabase.OpenRaw(temp.File("db.sqlite"));
    }

    public void Dispose() {
        connection.Dispose();
        temp.Dispose();
    }

    [Fact]
    public void ShouldApplyOnlyNewerMigrations_WhenDatabaseHasVersion() {
        Migrations.Migrate(connection, [first]);

        Migrations.Migrate(connection, [first, second]);

        Assert.Contains("extra", Tables());
    }

    [Fact]
    public void ShouldStoreLatestVersion_WhenMigrated() {
        Migrations.Migrate(connection, Migrations.All);

        Assert.Equal(Migrations.All[^1].Version, Migrations.CurrentVersion(connection));
    }

    [Fact]
    public async Task ShouldMigrateOnce_WhenConnectionsOpenNewDatabaseTogether() {
        string path = temp.File("shared.db");
        using Barrier start = new(openers);

        int[] versions = await Task.WhenAll(Enumerable.Range(0, openers).Select(_ => Task.Factory.StartNew(() => {
            start.SignalAndWait(TestContext.Current.CancellationToken);
            using SqliteConnection opened = Database.Open(path, NullLogger.Instance);
            return Migrations.CurrentVersion(opened);
        }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default)));

        Assert.Equal(Enumerable.Repeat(Migrations.All[^1].Version, openers), versions);
    }

    [Fact]
    public void ShouldRollBackWholeMigration_WhenStatementFails() {
        Migrations.Migrate(connection, [first]);
        Migration broken = new(2, ["CREATE TABLE half (id INTEGER PRIMARY KEY)", "CREATE TABLE (oops"]);

        MigrationException failed =
            Assert.Throws<MigrationException>(() => Migrations.Migrate(connection, [first, broken]));

        Assert.Equal((false, 1, "migration 2 failed and was rolled back"),
            (Tables().Contains("half"), Migrations.CurrentVersion(connection), failed.Message));
    }

    [Fact]
    public void ShouldRefuseDatabase_WhenSchemaIsNewerThanCode() {
        Migrations.Migrate(connection, [first, second]);

        SchemaTooNewException refused =
            Assert.Throws<SchemaTooNewException>(() => Migrations.Migrate(connection, [first]));

        Assert.Equal("database schema 2 is newer than this version supports (1)", refused.Message);
    }

    [Fact]
    public void ShouldBackfillFirstClassification_WhenUpgradingFromVersionOne() {
        Migrations.Migrate(connection, [Migrations.All[0]]);
        TestDatabase.Execute(connection,
            "INSERT INTO ads (id, source, source_id, title, company, url, dedup_key, first_seen, label, "
            + "classified_at) VALUES "
            + "('a', 'finn', '1', 't', 'c', 'u', 'k', 'seen-a', 'junior', 'checked-a'), "
            + "('b', 'finn', '2', 't', 'c', 'u', 'k', 'seen-b', 'junior', NULL), "
            + "('c', 'finn', '3', 't', 'c', 'u', 'k', 'seen-c', NULL, NULL)");

        Migrations.Migrate(connection, Migrations.All);

        Assert.Equal(["a|checked-a", "b|seen-b", "c|NULL"],
            TestDatabase.Rows(connection, "SELECT id, first_classified_at FROM ads ORDER BY id"));
    }

    private List<string> Tables() => TestDatabase.Rows(connection, "SELECT name FROM sqlite_master WHERE type = 'table'");
}
