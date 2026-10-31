using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Secrets;
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
    private const UnixFileMode readOnlyDir = UnixFileMode.UserRead | UnixFileMode.UserExecute;

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly Hashtable environment;

    public PairCommandTests() {
        environment = CommandFixtures.Environment(temp, Locales.English);
        environment["JOBRANGER_RELAY_URL"] = $"http://127.0.0.1:{ClosedPort()}";
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
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsSymlink() {
        string target = temp.File("target.db");
        File.WriteAllBytes(target, []);
        File.CreateSymbolicLink(DatabasePath(), target);

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, $"Cannot read the jobranger database at {DatabasePath()}."),
            (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintDirNotWritable_WhenDataFolderCannotBeMade() {
        string parent = temp.File("read-only");
        Directory.CreateDirectory(parent);
        File.SetUnixFileMode(parent, readOnlyDir);
        string data = Path.Combine(parent, "data");
        environment["JOBRANGER_DATA_DIR"] = data;
        string logs = Path.Combine(temp.Path, "state", "logs");

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure,
                $"Cannot create or use the folder {data}. Make it and the folders above it writable, then try again."),
            (exit, console.Output.TrimEnd()));
        string log = File.ReadAllText(Assert.Single(Directory.GetFiles(logs)));
        Assert.Contains("pairing.failed", log, StringComparison.Ordinal);
        Assert.Contains("dir_not_writable", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPrintSecretStoreFailed_WhenStoreDoesNotAnswer() {
        string logs = Path.Combine(temp.Path, "state", "logs");

        int exit = await RunAsync(
            inputRedirected: true,
            (_, _, _, _, _, _) => Task.FromException<Link>(new SecretStoreUnavailableException("link-keys", "timeout")));

        Assert.Equal(
            (CliExitCodes.Failure, "The secret store did not answer. Unlock it and try again."),
            (exit, console.Output.TrimEnd()));
        string log = Assert.Single(Directory.GetFiles(logs));
        Assert.Contains("pairing.failed", File.ReadAllText(log), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ShouldAllowKeychainPrompt_WhenInputIsTerminal(bool inputRedirected, bool expected) {
        bool? promptAllowed = null;

        await RunAsync(inputRedirected, (environment, paths, urls, logger, canPrompt, cancellationToken) => {
            promptAllowed = canPrompt;
            return Link.OpenAsync(environment, paths, urls, logger, canPrompt, cancellationToken);
        });

        Assert.Equal(expected, promptAllowed);
    }

    [Fact]
    public void ShouldOfferPairCommand_WhenRootIsBuilt() {
        RootCommand root = new(CliServices.Create(TimeProvider.System, console.Console, console.ErrorConsole, environment));

        Assert.Contains(PairCommand.CommandName, root.Subcommands.Select(command => command.Name));
    }

    private Task<int> RunAsync() => RunAsync(inputRedirected: true, Link.OpenAsync);

    private Task<int> RunAsync(bool inputRedirected, LinkCommandRun.LinkOpener openLink) {
        CliServices services = CliServices.Create(TimeProvider.System, console.Console, console.ErrorConsole, environment);
        System.CommandLine.RootCommand root =
            [new PairCommand(services, environment, TimeProvider.System, inputRedirected, openLink)];
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
