using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using RootCommand = Jobranger.Cli.Commands.RootCommand;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class PairCommandTests : IDisposable {
    private const string unreachable = "Cannot reach the jobranger relay. Check your connection and try again.";
    private const int corruptBytes = 4096;

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly Hashtable environment;

    public PairCommandTests() {
        environment = new Hashtable {
            ["HOME"] = temp.Path,
            ["LANG"] = Locales.English,
            ["JOBRANGER_CONFIG_DIR"] = Path.Combine(temp.Path, "config"),
            ["JOBRANGER_DATA_DIR"] = Path.Combine(temp.Path, "data"),
            ["JOBRANGER_STATE_DIR"] = Path.Combine(temp.Path, "state"),
            ["JOBRANGER_SECRET_STORE"] = "file",
            ["JOBRANGER_RELAY_URL"] = $"http://127.0.0.1:{ClosedPort()}",
        };
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task ShouldPrintRelayUnreachable_WhenRelayRefusesConnection() {
        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Failure, unreachable), (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintBadUrl_WhenRelayUrlIsInvalid() {
        environment["JOBRANGER_RELAY_URL"] = "ftp://relay.example.test";

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, $"JOBRANGER_RELAY_URL: {new EnglishMessages().ConfigNotServiceUrl}"),
            (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintDatabaseTooNew_WhenSchemaIsNewer() {
        using (SqliteConnection database = Database.Open(DatabasePath(), NullLogger.Instance)) {
            new StateStore(database).Set(StateStore.SchemaVersionKey, "99");
        }

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, "The database was made by a newer jobranger. Update jobranger and try again."),
            (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData(Locales.English, "Cannot read the jobranger database at {0}.")]
    [InlineData(Locales.Norwegian, "Kan ikke lese jobranger-databasen i {0}.")]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsCorrupt(string lang, string line) {
        environment["LANG"] = lang;
        File.WriteAllBytes(DatabasePath(), Encoding.ASCII.GetBytes(new string('x', corruptBytes)));

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Failure, string.Format(line, DatabasePath())), (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public void ShouldOfferPairCommand_WhenRootIsBuilt() {
        RootCommand root = new(CliServices.Create(TimeProvider.System, console.Console, console.ErrorConsole, environment));

        Assert.Contains(PairCommand.CommandName, root.Subcommands.Select(command => command.Name));
    }

    private Task<int> RunAsync() {
        CliServices services = CliServices.Create(TimeProvider.System, console.Console, console.ErrorConsole, environment);
        System.CommandLine.RootCommand root = [new PairCommand(services, environment, TimeProvider.System)];
        return root.Parse([PairCommand.CommandName]).InvokeAsync(null, TestContext.Current.CancellationToken);
    }

    private string DatabasePath() {
        string dataDir = Path.Combine(temp.Path, "data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "jobranger.db");
    }

    private static int ClosedPort() {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
