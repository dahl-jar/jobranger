using System.Collections;
using System.CommandLine;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Spectre.Console;
using RootCommand = Jobranger.Cli.Commands.RootCommand;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class LogsCommandTests : IDisposable {
    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly FakeTimeProvider clock = AdFixtures.Clock();
    private readonly Hashtable environment;
    private readonly Paths paths;

    public LogsCommandTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task ShouldPrintLastRunLines_WhenTwoRunsLogged() {
        long first = StartRun();
        long second = StartRun();
        Directory.CreateDirectory(paths.LogDir());
        File.WriteAllLines(CommandFixtures.LogPath(paths), [Line(first, "finn"), Line(second, "nav")]);

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Ok, "2026-10-09T10:00:00+00:00 info source.done source=nav count=2\n"),
            (exit, console.Output));
    }

    [Theory]
    [InlineData(Locales.English, "No runs yet. Run one with: jobranger run\n")]
    [InlineData(Locales.Norwegian, "Ingen kjøringer ennå. Start en med: jobranger run\n")]
    public async Task ShouldPrintNoRuns_WhenNoRunExists(string lang, string line) {
        environment["LANG"] = lang;

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output));
    }

    [Theory]
    [InlineData(Locales.English, "No log lines for the last run. Logs are kept for 14 days.\n")]
    [InlineData(Locales.Norwegian, "Ingen logglinjer for siste kjøring. Logger lagres i 14 dager.\n")]
    public async Task ShouldPrintLogsMissing_WhenLastRunHasNoLines(string lang, string line) {
        environment["LANG"] = lang;
        StartRun();

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintDatabaseTooNew_WhenSchemaIsNewer() {
        using (SqliteConnection database = Database.Open(paths.DbPath(), NullLogger.Instance)) {
            new StateStore(database).Set(StateStore.SchemaVersionKey, "99");
        }

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, "The database was made by a newer jobranger. Update jobranger and try again.\n", "logs.failed schema_too_new"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenMigrationFails() {
        CommandFixtures.RewindSchema(paths.DbPath());

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, $"Cannot read the jobranger database at {paths.DbPath()}.\n", "logs.failed migration_failed"),
            (exit, console.Output, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public async Task ShouldPrintFileUnreadable_WhenLogFileIsSymlink() {
        StartRun();
        Directory.CreateDirectory(paths.LogDir());
        File.CreateSymbolicLink(CommandFixtures.LogPath(paths), temp.File("elsewhere.jsonl"));

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Failure, $"Cannot read {CommandFixtures.LogPath(paths)}.\n"), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintFileAccessFailed_WhenLogFolderIsLocked() {
        StartRun();
        Directory.CreateDirectory(paths.LogDir());
        File.SetUnixFileMode(paths.LogDir(), UnixFileMode.None);

        int exit = await RunAsync();

        File.SetUnixFileMode(paths.LogDir(), FileModes.OwnerOnlyDir);
        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.FileAccessFailed),
            (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintFileAccessFailed_WhenStateFolderIsLocked() {
        StartRun();
        Directory.CreateDirectory(paths.LogDir());
        File.SetUnixFileMode(paths.StateDir(), UnixFileMode.None);

        int exit = await RunAsync();

        File.SetUnixFileMode(paths.StateDir(), FileModes.OwnerOnlyDir);
        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.FileAccessFailed),
            (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintFileAccessFailed_WhenLogFolderIsFile() {
        StartRun();
        Directory.CreateDirectory(paths.StateDir());
        File.WriteAllText(paths.LogDir(), string.Empty);

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.FileAccessFailed),
            (exit, console.Output));
    }

    [Fact]
    public async Task ShouldExitWithoutTrace_WhenOutputCannotBeWritten() {
        int exit = await RunAsync(CommandFixtures.BrokenConsole(typeof(IOException)));

        Assert.Equal(
            (CliExitCodes.Failure, string.Empty, "logs.failed output_failed"),
            (exit, console.Errors, CommandFixtures.LoggedFailure(paths)));
    }

    [Fact]
    public void ShouldOfferLogsCommand_WhenRootIsBuilt() {
        RootCommand root = new(CliServices.Create(clock, console.Console, console.ErrorConsole, environment));

        Assert.Contains(LogsCommand.CommandName, root.Subcommands.Select(command => command.Name));
    }

    private static string Line(long runId, string source) => new JsonObject {
        ["ts"] = "2026-10-09T10:00:00+00:00",
        ["level"] = "info",
        ["event"] = "source.done",
        ["run_id"] = runId.ToString(CultureInfo.InvariantCulture),
        ["source"] = source,
        ["count"] = 2,
    }.ToJsonString();

    private long StartRun() {
        using SqliteConnection connection = Database.Open(paths.DbPath(), NullLogger.Instance);
        return new RunStore(connection).Start(AdFixtures.Now);
    }

    private Task<int> RunAsync(IAnsiConsole? output = null) {
        CliServices services = CliServices.Create(clock, output ?? console.Console, console.ErrorConsole, environment);
        System.CommandLine.RootCommand root = [new LogsCommand(services, environment)];
        InvocationConfiguration configuration = new() { Error = console.ErrorConsole.Profile.Out.Writer };
        return root.Parse([LogsCommand.CommandName]).InvokeAsync(configuration, TestContext.Current.CancellationToken);
    }
}
