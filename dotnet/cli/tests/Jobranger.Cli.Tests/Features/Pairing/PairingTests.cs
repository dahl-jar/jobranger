using System.Collections;
using System.Net;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Jobranger.Messaging.Channels;
using Jobranger.Messaging.Envelopes;
using Jobranger.Messaging.Limits;
using Jobranger.Messaging.Payloads;
using Jobranger.Messaging.Runs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSec.Cryptography;
using Spectre.Console;
using LinkEntry = Jobranger.Cli.Features.Link.Link;
using PairingEntry = Jobranger.Cli.Features.Pairing.Pairing;
using SessionsEntry = Jobranger.Cli.Features.Sessions.Sessions;
using Status = Jobranger.Messaging.Runs.Status;

namespace Jobranger.Cli.Tests.Features.Pairing;

[UnsupportedOSPlatform("windows")]
public sealed class PairingTests : IAsyncDisposable {
    private const string stateDirVariable = "JOBRANGER_STATE_DIR";
    private const string yes = "y";
    private const string machine = "build-host";

    private static readonly DateTimeOffset now = DeviceFixtures.PairedAt;
    private static readonly TimeSpan stepTimeout = TimeSpan.FromSeconds(MessageLimits.StepTimeoutSeconds);
    private static readonly TimeSpan confirmTimeout = TimeSpan.FromSeconds(MessageLimits.PairConfirmTimeoutSeconds);
    private static readonly ServiceUrls urls = new(new Uri("https://site.example.test"), FakeRelay.Address);

    private readonly TempDir temp = new();
    private readonly TestDatabase db = new();
    private readonly FakeTimeProvider time = new(now);
    private readonly FakeRelay relay;
    private readonly HttpClient http;
    private readonly LinkEntry link;
    private readonly Paths paths;
    private readonly FakeTerminal terminal = new();
    private readonly StringWriter output = new();
    private readonly FakeBrowserPeer browser = new();
    private readonly FakeLogger logger = new();

    public PairingTests() {
        relay = new FakeRelay(time);
        http = relay.Client();
        Credentials credentials = new(
            new Hashtable(), null, new FileSecretStore(temp.File(SecretFixtures.FileName), NullLogger.Instance));
        LinkParts parts = new(new DeviceStore(db.Connection), new RelayApiClient(http, FakeRelay.Address, new FakeTimeProvider()), relay);
        link = new LinkEntry(credentials, temp.Path, NullLogger.Instance, parts);
        paths = new Paths(new Hashtable { [stateDirVariable] = temp.Path });
    }

    private string[] Lines => output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    public async ValueTask DisposeAsync() {
        terminal.Dispose();
        browser.Dispose();
        await link.DisposeAsync();
        http.Dispose();
        relay.Dispose();
        output.Dispose();
        db.Dispose();
        temp.Dispose();
    }

    [Fact]
    public async Task ShouldAddDevice_WhenWebsiteConfirmsBeforeTerminalYes() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);
        relay.Hello(browser.Sid, browser.Confirm());
        await terminal.AskedAsync();

        terminal.Answer(yes);

        Status frame = await StatusAsync(browser);
        Assert.Equal(
            (CliExitCodes.Ok, DeviceFixtures.BrowserName, $"\u2713 Paired with {DeviceFixtures.BrowserName}."),
            (await Finished(run), Assert.Single(link.Devices()).Name, Lines[^1]));
        Assert.Equal(
            new Status(AppVersion.Text, MessageLimits.ContractVersion, machine, "2026-10-25T09:30:00+00:00", null, null, null, false),
            frame);
    }

    [Fact]
    public async Task ShouldNameMachineComputer_WhenMachineNameHasNoAllowedCharacters() {
        Task<int> run = StartAsync(hostName: "\u001b\u0007");

        await PairAsync(browser);

        Status frame = await StatusAsync(browser);
        await Finished(run);
        Assert.Equal("computer", frame.Machine);
    }

    [Fact]
    public async Task ShouldPair_WhenTerminalYesComesBeforeWebsiteConfirm() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);
        await terminal.AskedAsync();
        terminal.Answer(yes);

        relay.Hello(browser.Sid, browser.Confirm());

        Assert.Equal((CliExitCodes.Ok, "data"), (await Finished(run), (await relay.NextFromCliAsync()).Kind));
    }

    [Fact]
    public async Task ShouldPrintPairPageAndCode_WhenPairingStarts() {
        Task<int> run = StartAsync();
        await ConnectedAsync();
        time.Advance(TimeSpan.FromMinutes(10));
        await Finished(run);

        Assert.Equal(
            [
                "Open this page in your browser:", "  https://site.example.test/pair", "", "and enter this code:",
                "  BCDF-GHJK", "", "The code expires in 10 minutes. Waiting for the website...",
            ],
            output.ToString().Split(Environment.NewLine)[..7]);
    }

    [Fact]
    public async Task ShouldStyleCodeLines_WhenTerminalSupportsAnsi() {
        using StringWriter styled = new();
        Task<int> run = StartAsync(console: Console(styled, AnsiSupport.Yes));
        await ConnectedAsync();
        time.Advance(TimeSpan.FromMinutes(10));
        await Finished(run);

        Assert.Equal(
            [
                "  \u001b[1mhttps://site.example.test/pair\u001b[0m",
                "  \u001b[1mBCDF-GHJK\u001b[0m",
                "\u001b[2mThe code expires in 10 minutes. Waiting for the website...\u001b[0m",
            ],
            styled.ToString().Split(Environment.NewLine).Where(line => line.Contains('\u001b')));
    }

    [Fact]
    public async Task ShouldSendAbort_WhenUserAnswersNo() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);
        await terminal.AskedAsync();

        terminal.Answer("n");

        Assert.Equal(
            (CliExitCodes.Failure, "abort", 0, "Not paired."),
            (await Finished(run), Steps(), link.Devices().Count, Lines[^1]));
    }

    [Fact]
    public async Task ShouldSendAbort_WhenUserCancelsAfterWords() {
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<int> run = StartAsync(cancellationToken: stop.Token);
        await RevealedAsync(browser);
        await terminal.AskedAsync();

        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finished(run));
        Assert.Equal("abort", Steps());
    }

    [Fact]
    public async Task ShouldEndPairing_WhenBrowserMacIsBad() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);

        relay.Hello(browser.Sid, browser.Confirm(new byte[32]));

        Assert.Equal(
            (CliExitCodes.Failure, "abort", "Pairing failed. Run jobranger pair again."),
            (await Finished(run), Steps(), Lines[^1]));
    }

    [Fact]
    public async Task ShouldAbortBoth_WhenSecondUnpairedCommitArrives() {
        Task<int> run = StartAsync();
        await OfferedAsync(browser);
        using FakeBrowserPeer second = new();

        relay.Hello(second.Sid, second.Commit(Tag()));

        Assert.Equal(
            (CliExitCodes.Failure, $"{second.Sid}:abort {browser.Sid}:abort"),
            (await Finished(run), string.Join(' ', relay.SentSoFar().Select(sent => $"{sent.Sid}:{FakeRelay.Step(sent)}"))));
    }

    [Fact]
    public async Task ShouldKeepPairing_WhenPairedDeviceCommits() {
        using FakeBrowserPeer known = new();
        await AddKnownAsync(known);
        Task<int> run = StartAsync();
        await ConnectedAsync();
        relay.Hello(known.Sid, known.Commit());
        Assert.Equal("offer", FakeRelay.Step(await relay.NextFromCliAsync()));

        await PairAsync(browser);

        Assert.Equal(CliExitCodes.Ok, await Finished(run));
    }

    [Fact]
    public async Task ShouldAbortPairedDevice_WhenItIsSilentAfterOffer() {
        using FakeBrowserPeer known = new();
        await AddKnownAsync(known);
        Task<int> run = StartAsync();
        await ConnectedAsync();
        relay.Hello(known.Sid, known.Commit());
        known.ReadOffer((await relay.NextFromCliAsync()).Ciphertext);
        await RevealedAsync(browser);
        await terminal.AskedAsync();

        time.Advance(stepTimeout);

        Envelope expired = await relay.NextFromCliAsync();
        relay.Hello(browser.Sid, browser.Confirm());
        terminal.Answer(yes);
        Assert.Equal((known.Sid, "abort", CliExitCodes.Ok), (expired.Sid, FakeRelay.Step(expired), await Finished(run)));
    }

    [Fact]
    public async Task ShouldAbortPairedDevice_WhenItIsSilentAfterReveal() {
        using FakeBrowserPeer known = new();
        await AddKnownAsync(known);
        Task<int> run = StartAsync();
        await OfferedAsync(known, null);
        await RevealAsync(known);

        time.Advance(stepTimeout);

        Envelope expired = await relay.NextFromCliAsync();
        await PairAsync(browser);
        Assert.Equal((known.Sid, "abort", CliExitCodes.Ok), (expired.Sid, FakeRelay.Step(expired), await Finished(run)));
    }

    [Fact]
    public async Task ShouldIgnoreCommit_WhenPairTagDiffers() {
        Task<int> run = StartAsync();
        await ConnectedAsync();
        using FakeBrowserPeer stranger = new();
        relay.Hello(stranger.Sid, stranger.Commit(SessionsEntry.PairTag("ZZZZ-ZZZZ", new ChannelId(relay.Channel))));

        await PairAsync(browser);

        Assert.Equal((CliExitCodes.Ok, 1), (await Finished(run), link.Devices().Count));
    }

    [Fact]
    public async Task ShouldEndPairing_WhenBrowserDataArrivesBeforeTerminalYes() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);
        relay.Hello(browser.Sid, browser.Confirm());
        (long seq, string ciphertext) = browser.Seal("{}");

        relay.Data(browser.Sid, seq, ciphertext);

        Assert.Equal((CliExitCodes.Failure, "abort"), (await Finished(run), Steps()));
    }

    [Fact]
    public async Task ShouldEndPairing_WhenRelayClosesCandidateSession() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);

        relay.CloseSessions(browser.Sid);

        Assert.Equal((CliExitCodes.Failure, ""), (await Finished(run), Steps()));
    }

    [Fact]
    public async Task ShouldEndPairing_WhenBrowserAbortsAfterOffer() {
        Task<int> run = StartAsync();
        await OfferedAsync(browser);

        relay.Hello(browser.Sid, FakeBrowserPeer.Encode("{\"t\":\"abort\"}"));

        Assert.Equal(CliExitCodes.Failure, await Finished(run));
    }

    [Fact]
    public async Task ShouldEndPairing_WhenBrowserIsSilentForStepTimeout() {
        Task<int> run = StartAsync();
        await OfferedAsync(browser);

        time.Advance(stepTimeout);

        Assert.Equal((CliExitCodes.Failure, "abort"), (await Finished(run), Steps()));
    }

    [Fact]
    public async Task ShouldPair_WhenWebsiteConfirmsJustInsideConfirmTimeout() {
        Task<int> run = StartAsync();
        await OfferedAsync(browser);
        time.Advance(TimeSpan.FromSeconds(10));
        await RevealAsync(browser);
        await terminal.AskedAsync();
        terminal.Answer(yes);
        time.Advance(TimeSpan.FromSeconds(119));

        relay.Hello(browser.Sid, browser.Confirm());

        Assert.Equal(CliExitCodes.Ok, await Finished(run));
    }

    [Fact]
    public async Task ShouldEndPairing_WhenWebsiteConfirmMissesConfirmTimeout() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);
        await terminal.AskedAsync();
        terminal.Answer(yes);
        await ConfirmReadAsync();

        time.Advance(confirmTimeout);

        Assert.Equal((CliExitCodes.Failure, "abort"), (await Finished(run), Steps()));
    }

    [Fact]
    public async Task ShouldEndPairing_WhenTerminalAnswerMissesConfirmTimeout() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);
        relay.Hello(browser.Sid, browser.Confirm());
        await ConfirmReadAsync();
        await terminal.AskedAsync();

        time.Advance(confirmTimeout);

        Assert.Equal((CliExitCodes.Failure, "abort", 0), (await Finished(run), Steps(), link.Devices().Count));
    }

    [Fact]
    public async Task ShouldPrintExpired_WhenCodeExpiresBeforeAnyBrowser() {
        Task<int> run = StartAsync();
        await ConnectedAsync();

        time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal((CliExitCodes.Failure, "The code expired. Run jobranger pair again."), (await Finished(run), Lines[^1]));
    }

    [Fact]
    public async Task ShouldPrintExpired_WhenFramesAreQueuedPastCodeExpiry() {
        relay.CodeLifetime = TimeSpan.Zero;
        using FakeBrowserPeer probe = new();
        relay.Hello(probe.Sid, probe.Reveal());
        relay.Hello(probe.Sid, probe.Reveal());

        int exit = await Finished(StartAsync());

        Assert.Equal((CliExitCodes.Failure, "", "The code expired. Run jobranger pair again."), (exit, Steps(), Lines[^1]));
    }

    [Fact]
    public async Task ShouldRefuseSecondPair_WhenLockIsHeld() {
        using FileLock held = FileLock.TryAcquire(paths.PairLockPath(), NullLogger.Instance)!;

        int exit = await Finished(StartAsync());

        Assert.Equal(
            (CliExitCodes.Failure, "Pairing is already running in another terminal.", 0),
            (exit, Lines[^1], relay.Routes.Count));
    }

    [Theory]
    [InlineData("Firefox\u001b[2J on\u202e macOS", "Firefox2J on macOS wants to pair.", "Firefox2J on macOS")]
    [InlineData("\u001b\u202e", "A browser wants to pair.", "a browser")]
    public async Task ShouldShowCleanName_WhenBrowserNameHasControlCharacters(string agent, string line, string stored) {
        using FakeBrowserPeer named = new(
            Key.Create(KeyAgreementAlgorithm.X25519),
            Key.Create(KeyAgreementAlgorithm.X25519),
            new byte[16],
            new byte[16],
            agent);
        Task<int> run = StartAsync();

        await PairAsync(named);

        await Finished(run);
        Assert.Equal((line, stored), (Lines[5], Assert.Single(link.Devices()).Name));
    }

    [Fact]
    public async Task ShouldStoreEnglishFallbackName_WhenNorwegianBrowserNameIsEmpty() {
        using FakeBrowserPeer unnamed = new(
            Key.Create(KeyAgreementAlgorithm.X25519),
            Key.Create(KeyAgreementAlgorithm.X25519),
            new byte[16],
            new byte[16],
            "\u001b\u202e");
        Task<int> run = StartAsync(new NorwegianMessages());

        await PairAsync(unnamed);

        await Finished(run);
        Assert.Equal(
            ("a browser", "En nettleser vil koble seg til.", "\u2713 Koblet til en nettleser."),
            (Assert.Single(link.Devices()).Name, Lines[5], Lines[^1]));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Cannot reach the jobranger relay. Check your connection and try again.")]
    [InlineData(HttpStatusCode.TooManyRequests, "The relay refused the pairing request. Try again in a minute.")]
    public async Task ShouldExplainRelayFailure_WhenPairStartFails(HttpStatusCode status, string line) {
        relay.Failures[FakeRelay.StartRoute] = FakeRelay.Problem(status, "x");

        int exit = await Finished(StartAsync());

        Assert.Equal((CliExitCodes.Failure, line), (exit, Lines[^1]));
    }

    [Fact]
    public async Task ShouldLogRefusalCode_WhenRelayRefusesPairing() {
        relay.Failures[FakeRelay.StartRoute] = FakeRelay.Problem(HttpStatusCode.TooManyRequests, "rate_limited");

        await Finished(StartAsync());

        Assert.Equal("relay refused pairing status=429 error_type=rate_limited", logger.LatestRecord.Message);
    }

    [Fact]
    public async Task ShouldPrintRelayUnreachable_WhenHubCannotConnect() {
        relay.ConnectFailure = new RelayUnavailableException("hub down");

        int exit = await Finished(StartAsync());

        Assert.Equal(
            (CliExitCodes.Failure, "Cannot reach the jobranger relay. Check your connection and try again."),
            (exit, Lines[^1]));
    }

    [Fact]
    public async Task ShouldPrintKeysBroken_WhenDevicesExistWithoutKeys() {
        link.AddDevice(browser.StaticPublic, DeviceFixtures.BrowserName, now);

        int exit = await Finished(StartAsync());

        Assert.Equal(
            (CliExitCodes.Failure, "The link keys cannot be used. Run jobranger unpair to make new ones."),
            (exit, Lines[^1]));
    }

    [Fact]
    public async Task ShouldEndPairing_WhenRelayDisconnects() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);

        relay.Disconnect();

        Assert.Equal((CliExitCodes.Failure, "Pairing failed. Run jobranger pair again."), (await Finished(run), Lines[^1]));
    }

    [Theory]
    [InlineData("\u001b]0;spoof\u0007", "unknown")]
    [InlineData("rate_limited", "rate_limited")]
    public async Task ShouldLogKnownReasonOnly_WhenHubRefusesFrame(string reason, string logged) {
        relay.SendFailure = new HubRefusedException(reason);
        Task<int> run = StartAsync();
        await ConnectedAsync();

        relay.Hello(browser.Sid, browser.Commit(Tag()));

        Assert.Equal(
            (CliExitCodes.Failure, "Pairing failed. Run jobranger pair again.", false,
                $"relay hub refused a pairing frame error_type={logged}"),
            (await Finished(run), Lines[^1], output.ToString().Contains('\u001b', StringComparison.Ordinal),
                logger.LatestRecord.Message));
    }

    [Fact]
    public async Task ShouldEndPairing_WhenAnotherCliReplacesLink() {
        Task<int> run = StartAsync();
        await RevealedAsync(browser);

        relay.Replace();

        Assert.Equal((CliExitCodes.Failure, "Pairing failed. Run jobranger pair again."), (await Finished(run), Lines[^1]));
    }

    private Task<int> StartAsync(
        IMessages? messages = null,
        string hostName = machine,
        IAnsiConsole? console = null,
        CancellationToken? cancellationToken = null) {
        PairingEntry pairing = new(
            link, messages ?? new EnglishMessages(), urls, paths, hostName, time, logger);
        return pairing.RunAsync(
            console ?? Console(output, AnsiSupport.No), terminal.Ask, cancellationToken ?? TestContext.Current.CancellationToken);
    }

    private static IAnsiConsole Console(StringWriter writer, AnsiSupport ansi) =>
        AnsiConsole.Create(new AnsiConsoleSettings {
            Ansi = ansi,
            ColorSystem = ColorSystemSupport.Standard,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
            EnvironmentVariables = [],
        });

    private static Task<int> Finished(Task<int> run) => run.WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);

    private string Steps() =>
        string.Join(' ', relay.SentSoFar().Select(sent => sent.Kind == "data" ? "data" : FakeRelay.Step(sent)));

    private byte[] Tag() => SessionsEntry.PairTag(FakeRelay.Code, new ChannelId(relay.Channel));

    private Task ConnectedAsync() => TestWaits.UntilAsync(() => relay.Handles.Count > 0);

    private async Task ConfirmReadAsync() {
        using FakeBrowserPeer probe = new();
        relay.Hello(probe.Sid, probe.Reveal());
        Assert.Equal("unknown_session", FakeRelay.Step(await relay.NextFromCliAsync()));
    }

    private Task OfferedAsync(FakeBrowserPeer peer) => OfferedAsync(peer, Tag());

    private async Task OfferedAsync(FakeBrowserPeer peer, byte[]? pairTag) {
        await ConnectedAsync();
        relay.Hello(peer.Sid, peer.Commit(pairTag));
        peer.ReadOffer((await relay.NextFromCliAsync()).Ciphertext);
    }

    private async Task<Status> StatusAsync(FakeBrowserPeer peer) {
        Envelope status = await relay.NextFromCliAsync();
        return PayloadCodec.Decode<Status>(Encoding.UTF8.GetBytes(peer.Open(status.Seq, status.Ciphertext)));
    }

    private async Task AddKnownAsync(FakeBrowserPeer known) {
        (await link.LoadOrCreateKeysAsync(TestContext.Current.CancellationToken)).Dispose();
        link.AddDevice(known.StaticPublic, DeviceFixtures.BrowserName, now);
    }

    private async Task RevealAsync(FakeBrowserPeer peer) {
        relay.Hello(peer.Sid, peer.Reveal());
        Assert.True(peer.AcceptsCliConfirm((await relay.NextFromCliAsync()).Ciphertext));
    }

    private async Task RevealedAsync(FakeBrowserPeer peer) {
        await OfferedAsync(peer);
        await RevealAsync(peer);
    }

    private async Task PairAsync(FakeBrowserPeer peer) {
        await RevealedAsync(peer);
        relay.Hello(peer.Sid, peer.Confirm());
        await terminal.AskedAsync();
        terminal.Answer(yes);
    }
}
