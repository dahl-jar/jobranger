using System.Collections;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Files;
using Jobranger.Cli.Http;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Tests.Http;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using ProvidersEntry = Jobranger.Cli.Features.Providers.Providers;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class RunCommandTests : IDisposable {
    private const string emptyJson = "{}";
    private const string countsLine = "0 ads read, 0 new, 0 matches.";
    private const string countsNorwegian = "0 annonser lest, 0 nye, 0 treff.";
    private const string readingBoth = "Reading NAV, FINN...";
    private const string readingNorwegian = "Leser NAV, FINN...";
    private const string savedForWebsite = "✓ Saved for the website";
    private const string skipLine = "AI checks skipped: no model or key. Set one with: jobranger init";
    private const string missingEnglish = "No config yet. Set up with: jobranger init";
    private const string readingNav = "Reading NAV...";
    private const int corruptBytes = 4096;
    private const int unusedPort = 1;
    private const string secretStoreFailed = "run.secret_store_failed";
    private const string stopForced = "run.stop_forced";
    private const string authRefused = "HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n";

    private static readonly TimeSpan invokeLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan stopLimit = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan forcedStopLimit = TimeSpan.FromMilliseconds(1500);

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly FakeTimeProvider clock = AdFixtures.Clock();
    private readonly FakeApiHandler resend = FakeApiHandler.Json(200, emptyJson);
    private readonly Hashtable environment;
    private readonly Paths paths;

    public RunCommandTests() {
        environment = RunCommandFixtures.Environment(temp, Locales.English);
        paths = new Paths(environment);
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config());
    }

    public void Dispose() {
        resend.Dispose();
        console.Dispose();
        temp.Dispose();
    }

    [Theory]
    [InlineData(Locales.English, readingBoth, countsLine, true, true, "✓ Sent to the website and to user@example.test")]
    [InlineData(Locales.English, readingBoth, countsLine, true, false, "✓ Sent to user@example.test")]
    [InlineData(Locales.English, readingBoth, countsLine, false, true, savedForWebsite)]
    [InlineData(Locales.English, readingBoth, countsLine, false, false, null)]
    [InlineData(Locales.Norwegian, readingNorwegian, countsNorwegian, true, true, "✓ Sendt til nettsiden og til user@example.test")]
    [InlineData(Locales.Norwegian, readingNorwegian, countsNorwegian, true, false, "✓ Sendt til user@example.test")]
    [InlineData(Locales.Norwegian, readingNorwegian, countsNorwegian, false, true, "✓ Lagret for nettsiden")]
    public async Task ShouldPrintOutcomeLine_WhenPassEnds(
        string lang, string reading, string counts, bool email, bool website, string? outcome) {
        environment["LANG"] = lang;
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(email ? DeliveryFixtures.ResendEmail : null, website));

        int exit = await InvokeAsync(Seams(NavOkFinnFails(), resend));

        string[] expected = outcome is null ? [reading, counts] : [reading, counts, outcome];
        Assert.Equal((CliExitCodes.Ok, Lines(expected)), (exit, Output()));
    }

    [Theory]
    [InlineData(false, 5, 2, new[] { "a" }, "5 ads read, 2 new, 1 match.")]
    [InlineData(false, 1, 1, new[] { "a", "b" }, "1 ad read, 1 new, 2 matches.")]
    [InlineData(true, 5, 2, new[] { "a" }, "5 annonser lest, 2 nye, 1 treff.")]
    [InlineData(true, 1, 1, new[] { "a", "b" }, "1 annonse lest, 1 ny, 2 treff.")]
    public void ShouldShowCounts_WhenPassEnds(bool norwegian, int adsRead, int newAds, string[] reported, string line) {
        RunResult result = new() { Status = RunStatus.Completed, AdsRead = adsRead, NewAds = newAds, ReportedIds = reported };
        IMessages messages = norwegian ? new NorwegianMessages() : new EnglishMessages();

        string counts = RunCommand.CountsLine(result, messages);

        Assert.Equal(line, counts);
    }

    [Theory]
    [InlineData(Locales.English, "✕ Email not sent: Resend answered 500.")]
    [InlineData(Locales.Norwegian, "✕ E-post ikke sendt: Resend svarte 500.")]
    public async Task ShouldExitOne_WhenEmailFails(string lang, string line) {
        environment["LANG"] = lang;
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(DeliveryFixtures.ResendEmail));

        int exit = await InvokeAsync(Seams(NavOkFinnFails(), FakeApiHandler.Json(500, emptyJson)));

        Assert.Equal((CliExitCodes.Failure, line), (exit, Output().Split(Environment.NewLine)[^1]));
    }

    [Theory]
    [InlineData(Locales.English, "Another run is in progress.")]
    [InlineData(Locales.Norwegian, "En annen kjøring pågår allerede.")]
    public async Task ShouldExitZero_WhenAnotherRunHoldsLock(string lang, string line) {
        environment["LANG"] = lang;
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(DeliveryFixtures.ResendEmail));
        using FileLock? held = FileLock.TryAcquire(paths.RunLockPath(), NullLogger.Instance);

        int exit = await InvokeAsync(Seams([ScriptedSource.Done("nav", RunFixtures.Batch("nav"))], resend));

        Assert.Equal((CliExitCodes.Ok, line, 0), (exit, Output(), resend.Requests.Count));
    }

    [Fact]
    public async Task ShouldExitOne_WhenAllSourcesFail() {
        int exit = await InvokeAsync(Seams([FailingFinn()], resend));

        Assert.Equal(
            (CliExitCodes.Failure, Lines("Reading FINN...", countsLine, "Could not read FINN.", savedForWebsite)),
            (exit, Output()));
    }

    [Theory]
    [InlineData(Locales.English, false, missingEnglish)]
    [InlineData(Locales.Norwegian, false, "Ingen oppsettsfil ennå. Sett opp med: jobranger init")]
    [InlineData(Locales.English, true, missingEnglish)]
    public async Task ShouldPrintConfigMissing_WhenFileIsAbsent(string lang, bool configFound, string line) {
        environment["LANG"] = lang;
        File.Delete(paths.ConfigPath());
        if (configFound) {
            AppConfig absentProfile = RunCommandFixtures.Config() with { Profile = new ProfileRef(temp.File("absent.yaml")) };
            RunCommandFixtures.Save(environment, absentProfile);
        }

        int exit = await InvokeAsync(Seams([], resend));

        Assert.Equal((CliExitCodes.Failure, line), (exit, Output()));
    }

    [Fact]
    public async Task ShouldPrintFieldError_WhenConfigIsInvalid() {
        File.WriteAllText(paths.ConfigPath(), "version: 1\nprofile:\n  path: a\nnotify:\n  website: maybe\n");

        int exit = await InvokeAsync(Seams([], resend));

        Assert.Equal(CliExitCodes.Failure, exit);
        Assert.Contains(new EnglishMessages().ConfigNotBoolean, console.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPrintDatabaseTooNew_WhenSchemaIsNewer() {
        using (SqliteConnection database = Database.Open(paths.DbPath(), NullLogger.Instance)) {
            new StateStore(database).Set(StateStore.SchemaVersionKey, "99");
        }

        int exit = await InvokeAsync(Seams([], resend));

        Assert.Equal((CliExitCodes.Failure, new EnglishMessages().DatabaseTooNew), (exit, Output()));
    }

    [Fact]
    public async Task ShouldPrintDatabaseUnreadable_WhenDatabaseIsCorrupt() {
        Directory.CreateDirectory(paths.DataDir());
        File.WriteAllBytes(paths.DbPath(), Encoding.ASCII.GetBytes(new string('x', corruptBytes)));

        int exit = await InvokeAsync(Seams([], resend));

        Assert.Equal((CliExitCodes.Failure, new EnglishMessages().DatabaseUnreadable(paths.DbPath())), (exit, Output()));
    }

    [Fact]
    public async Task ShouldPrintFileUnreadable_WhenDatabaseIsSymlink() {
        Directory.CreateDirectory(paths.DataDir());
        File.CreateSymbolicLink(paths.DbPath(), temp.File("elsewhere.db"));

        int exit = await InvokeAsync(Seams([], resend));

        Assert.Equal((CliExitCodes.Failure, $"Cannot read {paths.DbPath()}."), (exit, Output()));
    }

    [Fact]
    public async Task ShouldPrintSkipLine_WhenModelIsMissing() {
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(model: new ModelConfig()));

        int exit = await InvokeAsync(Seams([], resend) with { OpenModel = RunSeams.Production.OpenModel });

        Assert.Equal((CliExitCodes.Ok, Lines(countsLine, skipLine, savedForWebsite)), (exit, Output()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldSkipChecks_WhenSecretStoreBreaks(bool breaksOnOpen) {
        RunSeams seams = Seams([], resend) with {
            OpenModel = RunSeams.Production.OpenModel,
            OpenSecrets = (_, _, _) => breaksOnOpen
                ? throw new SecretStoreUnavailableException("store", FakeSecretStore.FailureReason)
                : Task.FromResult<ISecretStore?>(new FakeSecretStore { IsBroken = true }),
        };

        int exit = await InvokeAsync(seams);

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(skipLine, console.Output, StringComparison.Ordinal);
        JsonObject failure = Assert.Single(
            RunCommandFixtures.LogEntries(environment), entry => (string?)entry[LogFields.Event] == secretStoreFailed);
        Assert.Equal(nameof(SecretStoreUnavailableException), (string?)failure[LogFields.ErrorType]);
    }

    [Theory]
    [InlineData(nameof(ProviderName.Anthropic), true)]
    [InlineData(nameof(ProviderName.OpenAI), true)]
    [InlineData(nameof(ProviderName.OpenRouter), true)]
    [InlineData(nameof(ProviderName.Ollama), false)]
    public void ShouldUseProxy_WhenProviderIsHosted(string provider, bool expected) {
        using SocketsHttpHandler handler = RunSeams.CreateModelHandler(Enum.Parse<ProviderName>(provider));

        Assert.Equal(expected, handler.UseProxy);
    }

    [Fact]
    public async Task ShouldNotFollowRedirect_WhenModelAnswers302() {
        await using RawHttpServer target = new(authRefused);
        string redirect = $"HTTP/1.1 302 Found\r\nLocation: {target.Url("/")}\r\nContent-Length: 0\r\n\r\n";
        await using RawHttpServer server = new(redirect);
        RunCommandFixtures.SeedPendingAd(environment, clock);
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(model: RunCommandFixtures.Ollama(server.Port)));
        RunSeams seams = Seams([], resend) with { OpenModel = RunSeams.Production.OpenModel };

        await ApiClock.RunAsync(clock, InvokeAsync(seams));

        Assert.Equal((true, false), (server.Request.IsCompleted, target.Request.IsCompleted));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ShouldAllowKeychainPrompt_WhenInputIsTerminal(bool inputRedirected, bool expected) {
        bool? promptAllowed = null;
        RunSeams seams = Seams([], resend) with {
            OpenSecrets = (_, canPrompt, _) => {
                promptAllowed = canPrompt;
                return Task.FromResult<ISecretStore?>(null);
            },
        };

        await InvokeAsync(seams, clock, TestContext.Current.CancellationToken, inputRedirected);

        Assert.Equal(expected, promptAllowed);
    }

    [Fact]
    public async Task ShouldTagEmailFailureWithRunId_WhenSendFails() {
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(DeliveryFixtures.ResendEmail));

        await InvokeAsync(Seams(NavOkFinnFails(), FakeApiHandler.Json(500, emptyJson)));

        IReadOnlyList<JsonObject> entries = LogFiles.ReadRun(paths.LogDir(), "1");
        Assert.Contains("email.failed", entries.Select(entry => (string?)entry[LogFields.Event]));
    }

    [Fact]
    public async Task ShouldStopSourceRead_WhenCancelledBeforeHeaders() {
        await using RawHttpServer server = new(string.Empty);
        RunSeams seams = Seams([new HttpSource("nav", new Uri($"http://localhost:{server.Port}/"))], resend) with {
            CreateSourceClient = time => SafeHttp.CreateClient(new HostLimiter(time), time, _ => true),
        };

        await AssertCancelStopsAsync(seams, server.Request, readingNav, stopLimit);
    }

    [Fact]
    public async Task ShouldStopSourceRead_WhenCancelledDuringBody() {
        using StalledBodyHandler body = new([]);
        RunSeams seams = Seams([new HttpSource("nav", new Uri("http://localhost/"))], resend) with {
            CreateSourceClient = _ => new HttpClient(body, disposeHandler: false),
        };

        await AssertCancelStopsAsync(seams, body.Reading.Task, readingNav, stopLimit);
    }

    [Fact]
    public async Task ShouldStopModelCall_WhenCancelledDuringBody() {
        StalledBodyHandler body = new([]);
        RunCommandFixtures.SeedPendingAd(environment, clock);
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(model: RunCommandFixtures.Ollama(unusedPort)));
        RunSeams seams = Seams([], resend) with {
            OpenModel = (model, credentials, supportsEffort, time, token) =>
                new ProvidersEntry(() => body, time).CreateAsync(model, credentials.GetSecretAsync, supportsEffort, token),
        };

        await AssertCancelStopsAsync(seams, body.Reading.Task, string.Empty, stopLimit);
    }

    [Fact]
    public async Task ShouldRecordCancel_WhenSourceIgnoresToken() {
        ScriptedSource source = new("nav", _ => new TaskCompletionSource<SourceBatch>().Task);

        await AssertCancelStopsAsync(Seams([source], resend), source.Started.Task, readingNav, forcedStopLimit);
    }

    [Fact]
    public async Task ShouldExit130_WhenWorkBlocksThread() {
        using CancellationTokenSource cancel = new();
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        RecordingTime time = new();
        RunSeams seams = Seams([], resend) with {
            OpenModel = (_, _, _, _, token) => {
                entered.Set();
                release.Wait(invokeLimit, CancellationToken.None);
                throw new OperationCanceledException(token);
            },
        };
        Task<int> running = Task.Run(() => InvokeAsync(seams, time, cancel.Token), CancellationToken.None);
        try {
            Assert.True(entered.Wait(TestWaits.Step, TestContext.Current.CancellationToken));
            await cancel.CancelAsync();
            await time.WaitForAsync(forcedStopLimit);
            time.Advance(forcedStopLimit);

            int exit = await running;

            Assert.Equal(RunCommandFixtures.Interrupted, exit);
            Assert.Contains(
                stopForced, RunCommandFixtures.LogEntries(environment).Select(entry => (string?)entry[LogFields.Event]));
        } finally {
            release.Set();
        }
    }

    [Fact]
    public async Task ShouldExit130_WhenSendIsCancelled() {
        using CancellationTokenSource cancel = new();
        using FakeApiHandler hanging = FakeApiHandler.Hanging();
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(DeliveryFixtures.ResendEmail));
        Task<int> running = InvokeAsync(Seams(NavOkFinnFails(), hanging), clock, cancel.Token);
        Assert.True(await hanging.ArrivedAsync(TestWaits.Step));

        await cancel.CancelAsync();
        int exit = await running;

        Assert.Equal((RunCommandFixtures.Interrupted, Lines(readingBoth, countsLine)), (exit, Output()));
    }

    [Fact]
    public async Task ShouldDeliverNothing_WhenCancelledAfterPassEnds() {
        using CancellationTokenSource cancel = new();
        RunCommandFixtures.SeedPendingAd(environment, clock);
        RunCommandFixtures.Save(environment, RunCommandFixtures.Config(DeliveryFixtures.ResendEmail));
        RunSeams seams = Seams([], resend) with {
            OpenModel = (_, _, _, _, _) => Task.FromResult<IModelClient>(new FakeModelClient(async (_, _) => {
                await cancel.CancelAsync();
                return FakeModelClient.Reply(RunFixtures.JavaReply);
            })),
        };

        int exit = await InvokeAsync(seams, clock, cancel.Token);

        Assert.Equal((RunCommandFixtures.Interrupted, string.Empty, 0), (exit, Output(), resend.Requests.Count));
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

    private static ScriptedSource FailingFinn() => new("finn", _ => throw new SourceException("feed failed", null));

    private static IReadOnlyList<ISource> NavOkFinnFails() =>
        [ScriptedSource.Done("nav", RunFixtures.Batch("nav")), FailingFinn()];

    private static RunSeams Seams(IReadOnlyList<ISource> sources, FakeApiHandler handler) => RunSeams.Production with {
        EnabledSources = _ => sources,
        OpenModel = (_, _, _, _, _) => Task.FromResult<IModelClient>(FakeModelClient.Answering(RunFixtures.JavaReply)),
        Transport = DeliveryFixtures.TransportFor(() => handler),
    };

    private async Task AssertCancelStopsAsync(RunSeams seams, Task reached, string output, TimeSpan limit) {
        using CancellationTokenSource cancel = new();
        Task<int> running = InvokeAsync(seams, TimeProvider.System, cancel.Token);
        await reached.WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);
        Stopwatch elapsed = Stopwatch.StartNew();

        await cancel.CancelAsync();
        int exit = await running;

        elapsed.Stop();
        string rows = string.Join(',', RunCommandFixtures.RunRows(environment));
        Assert.Equal((RunCommandFixtures.Interrupted, RunCommandFixtures.CancelledRow, output), (exit, rows, Output()));
        Assert.InRange(elapsed.Elapsed, TimeSpan.Zero, limit);
    }

    private string Output() => console.Output.TrimEnd();

    private Task<int> InvokeAsync(RunSeams seams) => InvokeAsync(seams, clock, TestContext.Current.CancellationToken);

    private Task<int> InvokeAsync(RunSeams seams, TimeProvider time, CancellationToken cancellationToken, bool inputRedirected = true) {
        CliServices services = CliServices.Create(time, console.Console, console.ErrorConsole, environment);
        System.CommandLine.RootCommand root = [new RunCommand(services, environment, time, inputRedirected, seams)];
        return root.Parse([RunCommand.CommandName]).InvokeAsync(null, cancellationToken)
            .WaitAsync(invokeLimit, TestContext.Current.CancellationToken);
    }
}
