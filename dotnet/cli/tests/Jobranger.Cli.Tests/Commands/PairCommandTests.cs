using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging.Abstractions;
using RootCommand = Jobranger.Cli.Commands.RootCommand;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class PairCommandTests : IDisposable {
    private const string unreachable = "Cannot reach the jobranger relay. Check your connection and try again.";
    private const string keysBlocked =
        "Pairing cannot start because jobranger cannot open or change {0} (open failed errno=13). "
        + "Make that file and its folder readable and writable, then run jobranger pair again.";
    private const string keysBlockedNorwegian =
        "Tilkoblingen kan ikke starte fordi jobranger ikke kan åpne eller endre {0} (open failed errno=13). "
        + "Gjør filen og mappen lesbare og skrivbare, og kjør jobranger pair på nytt.";
    private const int corruptBytes = 4096;
    private const UnixFileMode readOnlyDir = UnixFileMode.UserRead | UnixFileMode.UserExecute;

    private static readonly TimeSpan busyLimit = TimeSpan.FromSeconds(30);

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
        CommandFixtures.MarkSchemaTooNew(DatabasePath());

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, "The database was made by a newer jobranger. Update jobranger and try again."),
            (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData(Locales.English, CommandFixtures.DatabaseUnreadable)]
    [InlineData(Locales.Norwegian, "Kan ikke lese jobranger-databasen i {0}.\n")]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsCorrupt(string lang, string line) {
        environment["LANG"] = lang;
        File.WriteAllBytes(DatabasePath(), Encoding.ASCII.GetBytes(new string('x', corruptBytes)));

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Failure, CommandFixtures.WithPath(line, DatabasePath())), (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenMigrationFails() {
        CommandFixtures.RewindSchema(DatabasePath());

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(CommandFixtures.DatabaseUnreadable, DatabasePath())),
            (exit, console.Output));
        string log = LogText();
        Assert.Contains("pairing.failed", log, StringComparison.Ordinal);
        Assert.Contains("migration_failed", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsSymlink() {
        string target = temp.File("target.db");
        File.WriteAllBytes(target, []);
        File.CreateSymbolicLink(DatabasePath(), target);

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(CommandFixtures.DatabaseUnreadable, DatabasePath())),
            (exit, console.Output));
    }

    [Fact]
    public async Task ShouldPrintFileUnreadable_WhenPairLockHasNoPermissions() {
        string lockPath = new Paths(environment).PairLockPath();
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        File.WriteAllBytes(lockPath, []);
        File.SetUnixFileMode(lockPath, UnixFileMode.None);

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Failure, $"Cannot read {lockPath}."), (exit, console.Output.TrimEnd()));
        string log = LogText();
        Assert.Contains("pairing.failed", log, StringComparison.Ordinal);
        Assert.Contains("file_unreadable", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPrintDirNotWritable_WhenDataFolderCannotBeMade() {
        string parent = temp.File("read-only");
        Directory.CreateDirectory(parent);
        File.SetUnixFileMode(parent, readOnlyDir);
        string data = Path.Combine(parent, "data");
        environment["JOBRANGER_DATA_DIR"] = data;

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, CommandFixtures.WithPath(CommandFixtures.DirNotWritable, data)),
            (exit, console.Output));
        string log = LogText();
        Assert.Contains("pairing.failed", log, StringComparison.Ordinal);
        Assert.Contains("dir_not_writable", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPrintSecretStoreFailed_WhenStoreDoesNotAnswer() {
        int exit = await RunAsync(
            inputRedirected: true,
            (_, _, _, _, _, _) => Task.FromException<Link>(new SecretStoreUnavailableException("link-keys", "timeout")));

        Assert.Equal(
            (CliExitCodes.Failure, "The secret store did not answer. Unlock it and try again."),
            (exit, console.Output.TrimEnd()));
        Assert.Contains("pairing.failed", LogText(), StringComparison.Ordinal);
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

    [Theory]
    [InlineData(Locales.English, SecretFixtures.FileName, keysBlocked)]
    [InlineData(Locales.Norwegian, SecretFixtures.FileName, keysBlockedNorwegian)]
    [InlineData(Locales.English, LinkMetadata.FileName, keysBlocked)]
    public async Task ShouldPrintPermissionFix_WhenKeysFileHasNoPermissions(string lang, string name, string line) {
        environment["LANG"] = lang;
        string keysFile = WriteConfigFile(name, "{}");
        File.SetUnixFileMode(keysFile, UnixFileMode.None);

        int exit = await RunAsync();

        Assert.Equal((CliExitCodes.Failure, CommandFixtures.WithPath(line, keysFile)), (exit, console.Output.TrimEnd()));
        Assert.Contains("keys_blocked", LogText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SecretFixtures.FileName)]
    [InlineData(LinkMetadata.FileName)]
    public async Task ShouldAdviseUnpair_WhenKeysFileIsNotJson(string name) {
        WriteConfigFile(name, "not json");

        int exit = await RunAsync();

        Assert.Equal(
            (CliExitCodes.Failure, "The link keys cannot be used. Run jobranger unpair to make new ones."),
            (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintRetry_WhenLinkKeysAreBusy() {
        string lockPath = Path.Combine(new Paths(environment).ConfigDir(), LinkMetadata.FileName + ".lock");
        using FileLock held = FileLock.TryAcquire(lockPath, NullLogger.Instance)!;

        int exit = await Task.Run(() => RunAsync(), TestContext.Current.CancellationToken)
            .WaitAsync(busyLimit, TestContext.Current.CancellationToken);

        Assert.Equal((CliExitCodes.Failure, "Pairing failed. Run jobranger pair again."), (exit, console.Output.TrimEnd()));
        Assert.Contains("keys_busy", LogText(), StringComparison.Ordinal);
    }

    private Task<int> RunAsync() => RunAsync(inputRedirected: true, Link.OpenAsync);

    private Task<int> RunAsync(bool inputRedirected, LinkCommandRun.LinkOpener openLink) {
        CliServices services = CliServices.Create(TimeProvider.System, console.Console, console.ErrorConsole, environment);
        System.CommandLine.RootCommand root =
            [new PairCommand(services, environment, TimeProvider.System, inputRedirected, openLink)];
        return root.Parse([PairCommand.CommandName]).InvokeAsync(null, TestContext.Current.CancellationToken);
    }

    private string WriteConfigFile(string name, string content) {
        string configDir = new Paths(environment).ConfigDir();
        Directory.CreateDirectory(configDir);
        string written = Path.Combine(configDir, name);
        File.WriteAllText(written, content);
        return written;
    }

    private string LogText() =>
        File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(temp.Path, "state", "logs"))));

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
