using System.Collections;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using RootCommand = Jobranger.Cli.Commands.RootCommand;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class DevicesCommandTests : IDisposable {
    private const string otherName = "Chrome on Linux";
    private const string seenAt = "2026-10-27T08:00:00+00:00";
    private const string tooNew = "The database was made by a newer jobranger. Update jobranger and try again.";
    private const string logFile = "2026-10-25.jsonl";
    private const int corruptBytes = 4096;

    private static readonly DateTimeOffset nextDay = DeviceFixtures.PairedAt.AddDays(1);
    private static readonly DateTimeOffset lateEvening = new(2026, 10, 25, 23, 30, 0, TimeSpan.Zero);

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly Hashtable environment;
    private readonly FakeTimeProvider time = new(DeviceFixtures.PairedAt);
    private PairCommand.LinkOpener openLink = Link.OpenAsync;
    private bool inputRedirected = true;

    public DevicesCommandTests() {
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        environment = new Hashtable {
            ["HOME"] = temp.Path,
            ["LANG"] = Locales.English,
            ["JOBRANGER_CONFIG_DIR"] = Path.Combine(temp.Path, "config"),
            ["JOBRANGER_DATA_DIR"] = Path.Combine(temp.Path, "data"),
            ["JOBRANGER_STATE_DIR"] = Path.Combine(temp.Path, "state"),
            ["JOBRANGER_SECRET_STORE"] = "file",
        };
    }

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Theory]
    [InlineData(Locales.English, "AQEBAQEB  Firefox on macOS  paired 2026-10-25  last seen 2026-10-27")]
    [InlineData(Locales.Norwegian, "AQEBAQEB  Firefox on macOS  koblet til 2026-10-25  sist sett 2026-10-27")]
    public async Task ShouldPrintBrowserLine_WhenBrowserIsPaired(string lang, string line) {
        environment["LANG"] = lang;
        SeedBrowser();
        using (SqliteConnection database = OpenDatabase()) {
            SqlCommands.Execute(database, null, "UPDATE devices SET last_seen = $seen", ("$seen", seenAt));
        }

        int exit = await RunAsync("devices", "list");

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintOneLineEach_WhenTwoBrowsersArePaired() {
        Seed(DeviceFixtures.OtherKey, otherName, nextDay);
        SeedBrowser();

        await RunAsync("devices", "list");

        Assert.Equal(
            [
                "AQEBAQEB  Firefox on macOS  paired 2026-10-25  last seen never",
                "AgICAgIC  Chrome on Linux  paired 2026-10-26  last seen never",
            ],
            console.Output.TrimEnd().Split(Environment.NewLine));
    }

    [Theory]
    [InlineData(Locales.English, "last seen never")]
    [InlineData(Locales.Norwegian, "sist sett aldri")]
    public async Task ShouldPrintNever_WhenBrowserWasNeverSeen(string lang, string ending) {
        environment["LANG"] = lang;
        SeedBrowser();

        await RunAsync("devices", "list");

        Assert.EndsWith(ending, console.Output.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPrintLocalDate_WhenUtcDayDiffers() {
        time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus-two", "plus-two"));
        Seed(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, lateEvening);

        await RunAsync("devices", "list");

        Assert.Contains("paired 2026-10-26", console.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Locales.English, "No paired browsers. Pair one with: jobranger pair")]
    [InlineData(Locales.Norwegian, "Ingen tilkoblede nettlesere. Koble til en med: jobranger pair")]
    public async Task ShouldPrintEmptyState_WhenNoBrowserIsPaired(string lang, string line) {
        environment["LANG"] = lang;

        int exit = await RunAsync("devices", "list");

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData(Locales.English, "✓ Revoked Firefox on macOS.")]
    [InlineData(Locales.Norwegian, "✓ Fjernet Firefox on macOS.")]
    public async Task ShouldRevokeOnlyThatBrowser_WhenPrefixIsUnique(string lang, string line) {
        environment["LANG"] = lang;
        SeedBrowser();
        string other = Seed(DeviceFixtures.OtherKey, otherName, nextDay);

        int exit = await RunAsync("devices", "revoke", "AQEB");

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output.TrimEnd()));
        Assert.Equal([other], Remaining());
    }

    [Theory]
    [InlineData(Locales.English, "zzzz", "No browser with id zzzz.")]
    [InlineData(Locales.Norwegian, "zzzz", "Ingen nettleser med id zzzz.")]
    [InlineData(Locales.English, "aqeb", "No browser with id aqeb.")]
    [InlineData(Locales.English, "", "No browser with id .")]
    public async Task ShouldExitFailure_WhenNoIdMatches(string lang, string id, string line) {
        environment["LANG"] = lang;
        SeedBrowser();

        int exit = await RunAsync("devices", "revoke", id);

        Assert.Equal((CliExitCodes.Failure, line), (exit, console.Output.TrimEnd()));
        Assert.Single(Remaining());
    }

    [Theory]
    [InlineData(
        Locales.English,
        "More than one browser has an id that starts with AQEB. Nothing was revoked. Use more of the id.")]
    [InlineData(
        Locales.Norwegian,
        "Flere nettlesere har en id som starter med AQEB. Ingenting ble fjernet. Bruk mer av id-en.")]
    public async Task ShouldRemoveNothing_WhenPrefixIsAmbiguous(string lang, string line) {
        environment["LANG"] = lang;
        SeedBrowser();
        Seed(DeviceFixtures.SiblingKey, otherName, nextDay);

        int exit = await RunAsync("devices", "revoke", "AQEB");

        Assert.Equal((CliExitCodes.Failure, line), (exit, console.Output.TrimEnd()));
        Assert.Equal(2, Remaining().Count);
    }

    [Theory]
    [InlineData(Locales.English, "✓ Unpaired 2 browser(s). The website can no longer reach this machine.")]
    [InlineData(Locales.Norwegian, "✓ Koblet fra 2 nettleser(e). Nettsiden kan ikke lenger nå denne maskinen.")]
    public async Task ShouldPrintCount_WhenUnpairing(string lang, string line) {
        environment["LANG"] = lang;
        SeedBrowser();
        Seed(DeviceFixtures.OtherKey, otherName, nextDay);

        int exit = await RunAsync("unpair");

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output.TrimEnd()));
        Assert.Empty(Remaining());
    }

    [Theory]
    [InlineData(Locales.English, "No paired browsers.")]
    [InlineData(Locales.Norwegian, "Ingen tilkoblede nettlesere.")]
    public async Task ShouldPrintNoBrowsers_WhenUnpairingNothing(string lang, string line) {
        environment["LANG"] = lang;

        int exit = await RunAsync("unpair");

        Assert.Equal((CliExitCodes.Ok, line), (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldMakeNewChannel_WhenUnpairing() {
        string before = await LoadChannelAsync();

        await RunAsync("unpair");

        Assert.NotEqual(before, RecordedChannel());
    }

    [Fact]
    public async Task ShouldMakeNewKeys_WhenLinkMetadataIsBroken() {
        Directory.CreateDirectory(Path.GetDirectoryName(MetadataPath())!);
        File.WriteAllText(MetadataPath(), "{\"store\": \"keyring\"}");

        await RunAsync("unpair");

        Assert.Equal(RecordedChannel(), await LoadChannelAsync());
    }

    [Fact]
    public async Task ShouldPrintAlreadyRunning_WhenPairHoldsLock() {
        SeedBrowser();
        using FileLock held = FileLock.TryAcquire(new Paths(environment).PairLockPath(), NullLogger.Instance)!;

        int exit = await RunAsync("unpair");

        Assert.Equal(
            (CliExitCodes.Failure, "Pairing is already running in another terminal."),
            (exit, console.Output.TrimEnd()));
        Assert.Single(Remaining());
    }

    [Fact]
    public async Task ShouldPrintKeysFailed_WhenRotationFails() {
        Directory.CreateDirectory(MetadataPath());

        int exit = await RunAsync("unpair");

        Assert.Equal(
            (CliExitCodes.Failure,
                "The browsers are removed, but new link keys could not be made. Run jobranger unpair again."),
            (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData(false, true, "unpair")]
    [InlineData(true, false, "unpair")]
    [InlineData(false, false, "devices", "list")]
    [InlineData(false, false, "devices", "revoke", "AQEB")]
    public async Task ShouldAllowKeychainPrompt_WhenUnpairingFromTerminal(
        bool redirected, bool expected, params string[] args) {
        inputRedirected = redirected;
        bool? promptAllowed = null;
        openLink = (environment, paths, urls, logger, canPrompt, cancellationToken) => {
            promptAllowed = canPrompt;
            return Link.OpenAsync(environment, paths, urls, logger, canPrompt, cancellationToken);
        };

        await RunAsync(args);

        Assert.Equal(expected, promptAllowed);
    }

    [Theory]
    [InlineData("devices", "list")]
    [InlineData("devices", "revoke", "AQEB")]
    [InlineData("unpair")]
    public async Task ShouldPrintDatabaseTooNew_WhenSchemaIsNewer(params string[] args) {
        MarkSchemaTooNew();

        int exit = await RunAsync(args);

        Assert.Equal((CliExitCodes.Failure, tooNew), (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData(Locales.English, "Cannot read the jobranger database at {0}.")]
    [InlineData(Locales.Norwegian, "Kan ikke lese jobranger-databasen i {0}.")]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsCorrupt(string lang, string line) {
        environment["LANG"] = lang;
        File.WriteAllBytes(DatabasePath(), Encoding.ASCII.GetBytes(new string('x', corruptBytes)));

        int exit = await RunAsync("devices", "list");

        Assert.Equal((CliExitCodes.Failure, string.Format(line, DatabasePath())), (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsSymlink() {
        string target = temp.File("target.db");
        File.WriteAllBytes(target, []);
        File.CreateSymbolicLink(DatabasePath(), target);

        int exit = await RunAsync("devices", "list");

        Assert.Equal(
            (CliExitCodes.Failure, $"Cannot read the jobranger database at {DatabasePath()}."),
            (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintBadUrl_WhenRelayUrlIsInvalid() {
        environment["JOBRANGER_RELAY_URL"] = "ftp://relay.example.test";

        int exit = await RunAsync("devices", "list");

        Assert.Equal(
            (CliExitCodes.Failure, $"JOBRANGER_RELAY_URL: {new EnglishMessages().ConfigNotServiceUrl}"),
            (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData(Locales.English, "The secret store did not answer. Unlock it and try again.")]
    [InlineData(Locales.Norwegian, "Hemmelighetslageret svarte ikke. Lås det opp og prøv igjen.")]
    public async Task ShouldPrintSecretStoreFailed_WhenStoreDoesNotAnswer(string lang, string line) {
        environment["LANG"] = lang;
        openLink = (_, _, _, _, _, _) =>
            Task.FromException<Link>(new SecretStoreUnavailableException("link-keys", "timeout"));

        int exit = await RunAsync("devices", "list");

        Assert.Equal((CliExitCodes.Failure, line), (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData("devices.revoked", "devices", "revoke", "AQEB")]
    [InlineData("devices.unpaired", "unpair")]
    public async Task ShouldLogEvent_WhenCommandRemovesBrowsers(string logEvent, params string[] args) {
        SeedBrowser();

        await RunAsync(args);

        Assert.Contains(logEvent, LoggedEvents());
    }

    [Fact]
    public async Task ShouldLogFailure_WhenSchemaIsNewer() {
        MarkSchemaTooNew();

        await RunAsync("devices", "list");

        Assert.Contains("devices.failed", LoggedEvents());
    }

    [Theory]
    [InlineData(DevicesCommand.CommandName)]
    [InlineData(UnpairCommand.CommandName)]
    public void ShouldOfferCommand_WhenRootIsBuilt(string name) {
        RootCommand root = new(Services());

        Assert.Contains(name, root.Subcommands.Select(command => command.Name));
    }

    [Fact]
    public async Task ShouldReturnUsageCode_WhenRevokeHasNoId() {
        RootCommand root = new(Services());

        int exit = await root.RunAsync(["devices", "revoke"], TestContext.Current.CancellationToken);

        Assert.Equal((CliExitCodes.Usage, ""), (exit, console.Output));
        Assert.Contains(new EnglishMessages().InvalidArguments, console.Errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPrintHelp_WhenCommandHasNoOptions() {
        RootCommand root = new(Services());

        int exit = await root.RunAsync(["unpair", "--help"], TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(new EnglishMessages().UnpairDescription, console.Output, StringComparison.Ordinal);
    }

    private CliServices Services() => CliServices.Create(time, console.Console, console.ErrorConsole, environment);

    private Task<int> RunAsync(params string[] args) {
        CliServices services = Services();
        System.CommandLine.RootCommand root = [
            new DevicesCommand(services, environment, time, openLink),
            new UnpairCommand(services, environment, inputRedirected, openLink),
        ];
        return root.Parse(args).InvokeAsync(null, TestContext.Current.CancellationToken);
    }

    private SqliteConnection OpenDatabase() => Database.Open(DatabasePath(), NullLogger.Instance);

    private void SeedBrowser() => Seed(DeviceFixtures.BrowserKey, DeviceFixtures.BrowserName, DeviceFixtures.PairedAt);

    private string Seed(byte[] key, string name, DateTimeOffset at) {
        using SqliteConnection database = OpenDatabase();
        return new DeviceStore(database).Add(key, name, at);
    }

    private IReadOnlyList<string> Remaining() {
        using SqliteConnection database = OpenDatabase();
        return [.. new DeviceStore(database).List().Select(device => device.Id)];
    }

    private void MarkSchemaTooNew() {
        using SqliteConnection database = OpenDatabase();
        new StateStore(database).Set(StateStore.SchemaVersionKey, "99");
    }

    private async Task<string> LoadChannelAsync() {
        await using Link link = await Link.OpenAsync(
            environment,
            new Paths(environment),
            ServiceUrls.From(environment),
            NullLogger.Instance,
            canPrompt: false,
            TestContext.Current.CancellationToken);
        using LinkKeyPair keys = await link.LoadOrCreateKeysAsync(TestContext.Current.CancellationToken);
        return keys.Channel.Value;
    }

    private string RecordedChannel() =>
        LinkMetadata.Read(new FileSecretStore(MetadataPath(), NullLogger.Instance))!.Channel;

    private string MetadataPath() => Path.Combine(temp.Path, "config", LinkMetadata.FileName);

    private string[] LoggedEvents() =>
        [.. LogLines.Read(Path.Combine(temp.Path, "state", "logs", logFile)).Select(line => (string)line["event"]!)];

    private string DatabasePath() {
        string dataDir = Path.Combine(temp.Path, "data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "jobranger.db");
    }
}
