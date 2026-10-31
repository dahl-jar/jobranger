using System.Collections;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
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
    private const string unreadable = "Cannot read the jobranger database at {0}.";
    private const string keysFileFailed =
        "New link keys could not be made because jobranger cannot use {0} ({1}). "
        + "Fix or remove that file, then run jobranger unpair again.";
    private const string keysFileFailedNorwegian =
        "Nye koblingsnøkler kunne ikke lages fordi jobranger ikke kan bruke {0} ({1}). "
        + "Rett eller fjern filen, og kjør jobranger unpair på nytt.";
    private const string keysWriteOpening =
        "New link keys could not be made because jobranger cannot change {0} (";
    private const string keysWriteOpeningNorwegian =
        "Nye koblingsnøkler kunne ikke lages fordi jobranger ikke kan endre {0} (";
    private const string keysWriteAdvice =
        "). Make that file and its folder readable and writable, then run jobranger unpair again.";
    private const string keysWriteAdviceNorwegian =
        "). Gjør filen og mappen lesbare og skrivbare, og kjør jobranger unpair på nytt.";
    private const string dirNotWritable =
        "Cannot create or use the folder {0}. Make it and the folders above it writable, then try again.";
    private const string dirNotWritableNorwegian =
        "Kan ikke lage eller bruke mappen {0}. Gjør den og mappene over den skrivbare, og prøv igjen.";
    private const string dirFailure = "dir_not_writable";
    private const string notJsonReason = "not UTF-8 JSON";
    private const string notStringsReason = "not a JSON object of strings";
    private const string savedSecrets = "{\"smtp_password\": \"fake-password\"}";
    private const string logFile = "2026-10-25.jsonl";
    private const int corruptBytes = 4096;
    private const UnixFileMode readOnlyDir = UnixFileMode.UserRead | UnixFileMode.UserExecute;

    private static readonly DateTimeOffset nextDay = DeviceFixtures.PairedAt.AddDays(1);
    private static readonly DateTimeOffset lateEvening = new(2026, 10, 25, 23, 30, 0, TimeSpan.Zero);

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly Hashtable environment;
    private readonly FakeTimeProvider time = new(DeviceFixtures.PairedAt);
    private LinkCommandRun.LinkOpener openLink = Link.OpenAsync;
    private bool inputRedirected = true;
    private bool? promptAllowed;

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

    [Fact]
    public async Task ShouldPrintNever_WhenBrowserWasNeverSeen() {
        environment["LANG"] = Locales.Norwegian;
        SeedBrowser();

        await RunAsync("devices", "list");

        Assert.EndsWith("sist sett aldri", console.Output.TrimEnd(), StringComparison.Ordinal);
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
    [InlineData(Locales.English, 1, "✓ Unpaired 1 browser. The website can no longer reach this machine.")]
    [InlineData(Locales.English, 2, "✓ Unpaired 2 browsers. The website can no longer reach this machine.")]
    [InlineData(Locales.Norwegian, 1, "✓ Koblet fra 1 nettleser. Nettsiden kan ikke lenger nå denne maskinen.")]
    [InlineData(Locales.Norwegian, 2, "✓ Koblet fra 2 nettlesere. Nettsiden kan ikke lenger nå denne maskinen.")]
    public async Task ShouldPrintCount_WhenUnpairing(string lang, int browsers, string line) {
        environment["LANG"] = lang;
        byte[][] keys = [DeviceFixtures.BrowserKey, DeviceFixtures.OtherKey];
        foreach (byte[] key in keys.Take(browsers)) {
            Seed(key, otherName, nextDay);
        }

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
        SeedBrowser();

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
        string lockPath = new Paths(environment).PairLockPath();
        using FileLock held = FileLock.TryAcquire(lockPath, NullLogger.Instance)!;

        int exit = await RunAsync("unpair");

        Assert.Equal(
            (CliExitCodes.Failure, "Pairing is already running in another terminal."),
            (exit, console.Output.TrimEnd()));
        Assert.Single(Remaining());
    }

    [Theory]
    [InlineData(Locales.English, "Cannot read {0}.")]
    [InlineData(Locales.Norwegian, "Kan ikke lese {0}.")]
    public async Task ShouldPrintFileUnreadable_WhenPairLockIsSymlink(string lang, string line) {
        environment["LANG"] = lang;
        SeedBrowser();
        string lockPath = new Paths(environment).PairLockPath();
        string target = temp.File("target.lock");
        File.WriteAllBytes(target, []);
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        File.CreateSymbolicLink(lockPath, target);

        int exit = await RunAsync("unpair");

        Assert.Equal((CliExitCodes.Failure, string.Format(line, lockPath)), (exit, console.Output.TrimEnd()));
        Assert.Single(Remaining());
        Assert.Contains("lock_unreadable", LoggedFailures());
    }

    [Theory]
    [InlineData(Locales.English, keysWriteOpening, keysWriteAdvice)]
    [InlineData(Locales.Norwegian, keysWriteOpeningNorwegian, keysWriteAdviceNorwegian)]
    public async Task ShouldPrintKeysFailed_WhenRotationFails(string lang, string opening, string advice) {
        environment["LANG"] = lang;
        SeedBrowser();
        Directory.CreateDirectory(MetadataPath());

        int exit = await RunAsync("unpair");

        Assert.Equal(CliExitCodes.Failure, exit);
        Assert.StartsWith(WithPath(opening, MetadataPath()), console.Output, StringComparison.Ordinal);
        Assert.EndsWith(advice, console.Output.TrimEnd(), StringComparison.Ordinal);
        Assert.Empty(Remaining());
    }

    [Theory]
    [InlineData(Locales.English, "not json", notJsonReason, keysFileFailed)]
    [InlineData(Locales.Norwegian, "not json", notJsonReason, keysFileFailedNorwegian)]
    [InlineData(Locales.English, "[]", notStringsReason, keysFileFailed)]
    [InlineData(Locales.English, "{\"smtp_password\": 1234}", notStringsReason, keysFileFailed)]
    [InlineData(Locales.English, "{\"smtp_password\": \"\\ud800\"}", notJsonReason, keysFileFailed)]
    public async Task ShouldAdviseRemoval_WhenSecretsContentIsInvalid(
        string lang, string content, string reason, string line) {
        environment["LANG"] = lang;
        string secrets = WriteSecrets(content);

        int exit = await RunAsync("unpair");

        Assert.Equal(
            (CliExitCodes.Failure, WithReason(line, secrets, reason)),
            (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldNotAdviseRemoval_WhenSecretsFileIsSymlink() {
        string secrets = SecretsPath();
        string target = temp.File("target.json");
        File.WriteAllText(target, savedSecrets);
        Directory.CreateDirectory(Path.GetDirectoryName(secrets)!);
        File.CreateSymbolicLink(secrets, target);

        int exit = await RunAsync("unpair");

        Assert.Equal(
            (CliExitCodes.Failure, WithPath(keysWriteOpening, secrets) + "symbolic link" + keysWriteAdvice),
            (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData(Locales.English, keysWriteOpening, keysWriteAdvice, "remove")]
    [InlineData(Locales.Norwegian, keysWriteOpeningNorwegian, keysWriteAdviceNorwegian, "fjern")]
    public async Task ShouldNotAdviseRemoval_WhenSecretsFileHasNoReadPermission(
        string lang, string opening, string advice, string removal) {
        environment["LANG"] = lang;
        string secrets = WriteSecrets(savedSecrets);
        File.SetUnixFileMode(secrets, UnixFileMode.None);

        int exit = await RunAsync("unpair");

        File.SetUnixFileMode(secrets, PrivateFile.OwnerOnlyFileMode);
        Assert.Equal(CliExitCodes.Failure, exit);
        Assert.StartsWith(WithPath(opening, secrets), console.Output, StringComparison.Ordinal);
        Assert.EndsWith(advice, console.Output.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain(removal, console.Output, StringComparison.Ordinal);
        Assert.Equal(savedSecrets, File.ReadAllText(secrets));
    }

    [Theory]
    [InlineData(Locales.English, keysWriteOpening, keysWriteAdvice, "remove")]
    [InlineData(Locales.Norwegian, keysWriteOpeningNorwegian, keysWriteAdviceNorwegian, "fjern")]
    public async Task ShouldNotAdviseRemoval_WhenConfigDirIsReadOnly(
        string lang, string opening, string advice, string removal) {
        environment["LANG"] = lang;

        int exit = await UnpairWithReadOnlyConfigAsync();

        Assert.Equal(CliExitCodes.Failure, exit);
        Assert.StartsWith(WithPath(opening, SecretsPath()), console.Output, StringComparison.Ordinal);
        Assert.EndsWith(advice, console.Output.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain(removal, console.Output, StringComparison.Ordinal);
        Assert.Equal(savedSecrets, File.ReadAllText(SecretsPath()));
    }

    [Fact]
    public async Task ShouldLogReason_WhenSecretsFileIsInvalid() {
        WriteSecrets("not json");

        await RunAsync("unpair");

        Assert.Contains($"keys reason={notJsonReason}", LoggedFailures());
    }

    [Fact]
    public async Task ShouldLogReasonWithoutFolder_WhenConfigDirIsReadOnly() {
        await UnpairWithReadOnlyConfigAsync();

        Assert.Contains(
            LoggedFailures(),
            failure => failure?.StartsWith("keys reason=Access to the path '/.secrets.json.", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(Locales.English, "New link keys could not be made. Run jobranger unpair again.")]
    [InlineData(Locales.Norwegian, "Nye koblingsnøkler kunne ikke lages. Kjør jobranger unpair på nytt.")]
    public async Task ShouldPrintRetry_WhenLinkKeysAreBusy(string lang, string line) {
        environment["LANG"] = lang;
        string lockPath = MetadataPath() + ".lock";
        using FileLock held = FileLock.TryAcquire(lockPath, NullLogger.Instance)!;

        int exit = await RunAsync("unpair");

        Assert.Equal((CliExitCodes.Failure, line), (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldLogFailure_WhenRotationFails() {
        Directory.CreateDirectory(MetadataPath());

        await RunAsync("unpair");

        Assert.Contains("devices.failed", LoggedEvents());
    }

    [Fact]
    public async Task ShouldLogRemoval_WhenRotationFails() {
        SeedBrowser();
        Directory.CreateDirectory(MetadataPath());

        await RunAsync("unpair");

        Assert.Contains("devices.unpaired", LoggedEvents());
    }

    [Fact]
    public async Task ShouldLogNoRemoval_WhenUnpairingNothing() {
        await RunAsync("unpair");

        Assert.DoesNotContain("devices.unpaired", LoggedEvents());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ShouldAllowKeychainPrompt_WhenUnpairingFromTerminal(bool redirected, bool expected) {
        inputRedirected = redirected;
        RecordPromptAllowed();

        await RunAsync("unpair");

        Assert.Equal(expected, promptAllowed);
    }

    [Theory]
    [InlineData("devices", "list")]
    [InlineData("devices", "revoke", "AQEB")]
    public async Task ShouldRefuseKeychainPrompt_WhenListingOrRevoking(params string[] args) {
        inputRedirected = false;
        RecordPromptAllowed();

        await RunAsync(args);

        Assert.False(promptAllowed);
    }

    [Fact]
    public async Task ShouldPrintDatabaseTooNew_WhenSchemaIsNewer() {
        MarkSchemaTooNew();

        int exit = await RunAsync("devices", "list");

        Assert.Equal(
            (CliExitCodes.Failure, "The database was made by a newer jobranger. Update jobranger and try again."),
            (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsCorrupt() {
        File.WriteAllBytes(DatabasePath(), Encoding.ASCII.GetBytes(new string('x', corruptBytes)));

        int exit = await RunAsync("devices", "list");

        Assert.Equal((CliExitCodes.Failure, WithPath(unreadable, DatabasePath())), (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsSymlink() {
        string target = temp.File("target.db");
        File.WriteAllBytes(target, []);
        File.CreateSymbolicLink(DatabasePath(), target);

        int exit = await RunAsync("devices", "list");

        Assert.Equal((CliExitCodes.Failure, WithPath(unreadable, DatabasePath())), (exit, console.Output.TrimEnd()));
    }

    [Theory]
    [InlineData(Locales.English, dirNotWritable)]
    [InlineData(Locales.Norwegian, dirNotWritableNorwegian)]
    public async Task ShouldPrintDirNotWritable_WhenDataFolderCannotBeMade(string lang, string line) {
        environment["LANG"] = lang;
        string data = FolderUnderReadOnlyParent();
        environment["JOBRANGER_DATA_DIR"] = data;

        int exit = await RunAsync("devices", "list");

        Assert.Equal((CliExitCodes.Failure, WithPath(line, data)), (exit, console.Output.TrimEnd()));
        Assert.Contains(dirFailure, LoggedFailures());
    }

    [Fact]
    public async Task ShouldPrintDirNotWritable_WhenDataFolderIsFile() {
        string data = temp.File("data-file");
        File.WriteAllBytes(data, []);
        environment["JOBRANGER_DATA_DIR"] = data;

        int exit = await RunAsync("devices", "list");

        Assert.Equal((CliExitCodes.Failure, WithPath(dirNotWritable, data)), (exit, console.Output.TrimEnd()));
    }

    [Fact]
    public async Task ShouldKeepBrowsers_WhenStateFolderCannotBeMade() {
        SeedBrowser();
        string state = FolderUnderReadOnlyParent();
        environment["JOBRANGER_STATE_DIR"] = state;

        int exit = await RunAsync("unpair");

        Assert.Equal((CliExitCodes.Failure, WithPath(dirNotWritable, state)), (exit, console.Output.TrimEnd()));
        Assert.Single(Remaining());
    }

    [Fact]
    public async Task ShouldKeepSystemReason_WhenConfigFolderCannotBeMade() {
        string config = FolderUnderReadOnlyParent();
        environment["JOBRANGER_CONFIG_DIR"] = config;

        await RunAsync("unpair");

        Assert.Contains(
            $"{Path.Combine(config, LinkMetadata.FileName)} (Access to the path",
            console.Output,
            StringComparison.Ordinal);
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

    private static string WithPath(string line, string path) => string.Format(line, path);

    private static string WithReason(string line, string path, string reason) => string.Format(line, path, reason);

    private CliServices Services() => CliServices.Create(time, console.Console, console.ErrorConsole, environment);

    private Task<int> RunAsync(params string[] args) {
        CliServices services = Services();
        System.CommandLine.RootCommand root = [
            new DevicesCommand(services, environment, time, openLink),
            new UnpairCommand(services, environment, inputRedirected, openLink),
        ];
        return root.Parse(args).InvokeAsync(null, TestContext.Current.CancellationToken);
    }

    private async Task<int> UnpairWithReadOnlyConfigAsync() {
        string secrets = WriteSecrets(savedSecrets);
        string config = Path.GetDirectoryName(secrets)!;
        File.WriteAllBytes(secrets + ".lock", []);
        File.WriteAllBytes(MetadataPath() + ".lock", []);
        File.SetUnixFileMode(config, readOnlyDir);
        try {
            return await RunAsync("unpair");
        } finally {
            File.SetUnixFileMode(config, FileModes.OwnerOnlyDir);
        }
    }

    private string FolderUnderReadOnlyParent() {
        string parent = temp.File("read-only");
        Directory.CreateDirectory(parent);
        File.SetUnixFileMode(parent, readOnlyDir);
        return Path.Combine(parent, "folder");
    }

    private string WriteSecrets(string content) {
        string secrets = SecretsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(secrets)!);
        File.WriteAllText(secrets, content);
        return secrets;
    }

    private string SecretsPath() => new Paths(environment).SecretsPath();

    private void RecordPromptAllowed() =>
        openLink = (environment, paths, urls, logger, canPrompt, cancellationToken) => {
            promptAllowed = canPrompt;
            return Link.OpenAsync(environment, paths, urls, logger, canPrompt, cancellationToken);
        };

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

    private string RecordedChannel() {
        FileSecretStore store = new(MetadataPath(), NullLogger.Instance);
        LinkMetadata metadata = LinkMetadata.Read(store)!;
        return metadata.Channel;
    }

    private string MetadataPath() => Path.Combine(temp.Path, "config", LinkMetadata.FileName);

    private string[] LoggedEvents() => [.. LogEntries().Select(line => (string)line["event"]!)];

    private string?[] LoggedFailures() => [.. LogEntries().Select(line => (string?)line["failure"])];

    private IReadOnlyList<JsonObject> LogEntries() =>
        LogLines.Read(Path.Combine(temp.Path, "state", "logs", logFile));

    private string DatabasePath() {
        string dataDir = Path.Combine(temp.Path, "data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "jobranger.db");
    }
}
