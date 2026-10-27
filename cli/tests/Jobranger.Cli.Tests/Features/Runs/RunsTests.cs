using System.Runtime.Versioning;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Files;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static Jobranger.Cli.Tests.Mocks.RunFixtures;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;

namespace Jobranger.Cli.Tests.Features.Runs;

[UnsupportedOSPlatform("windows")]
public sealed class RunsTests : IDisposable {
    private const string runsQuery = "SELECT status, finished_at IS NOT NULL FROM runs";
    private const string applyUrl = "https://apply.example.test/jobs/1";

    private static readonly string profilePath = MatchingFixtures.TestData("profile_user.yaml");
    private static readonly string javaReply = MatchingCases.Replies()["java_project"];
    private static readonly EmployerFeed feed = new("teamtailor", "computas", "Computas", "https://careers.example.test/jobs.rss");

    private readonly TestDatabase db = new();
    private readonly TempDir dir = new();
    private readonly Paths paths;
    private readonly FakeTimeProvider time = AdFixtures.Clock();
    private readonly AdsEntry ads;

    public RunsTests() {
        paths = TempPaths(dir);
        ads = new AdsEntry(db.Connection, time);
    }

    public void Dispose() {
        db.Dispose();
        dir.Dispose();
    }

    [Fact]
    public async Task ShouldSkipRun_WhenLockIsHeld() {
        using FileLock? held = FileLock.TryAcquire(paths.RunLockPath(), NullLogger.Instance);

        RunResult result = await RunAsync(Answering(), [], null, null, TestContext.Current.CancellationToken);

        Assert.Equal((RunStatus.Locked, (long?)null, 0), (result.Status, result.RunId, RunRows().Count));
    }

    [Fact]
    public async Task ShouldUseLocalDay_WhenLocalAndUtcDatesDiffer() {
        time.SetUtcNow(new DateTimeOffset(2026, 10, 26, 23, 30, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus-two", "plus-two"));
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.Upsert(Pending(2, "Tester", "Org Two AS") with { Deadline = "2026-10-26" });
        db.Execute("UPDATE ads SET label = 'junior', is_programming_job = 0 WHERE id = 'finn:2'");
        FakeModelClient client = Answering();

        await RunAsync(client, [], null, null, TestContext.Current.CancellationToken);

        Assert.Equal(
            (true, "deadline"),
            (client.Prompts[0].User.StartsWith("Assessment date: 2026-10-27", StringComparison.Ordinal),
                (string?)db.Scalar("SELECT closed_reason FROM ads WHERE id = 'finn:2'")));
    }

    [Fact]
    public async Task ShouldEndCancelled_WhenFetchIsCancelled() {
        using CancellationTokenSource run = new();
        ScriptedSource source = new("nav", async _ => {
            await run.CancelAsync();
            return Batch("nav");
        });
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.SetClassification("finn:1", AdFixtures.Classification, "old-version");
        ads.Upsert(Pending(2, "Tester", "Org Two AS"));
        FakeModelClient client = Answering();

        RunResult result = await RunAsync(client, [source], null, null, run.Token);

        Assert.Equal(
            (RunStatus.Cancelled, 0, "cancelled|1", "junior"),
            (result.Status, client.Calls, string.Join(',', RunRows()), (string?)db.Scalar("SELECT label FROM ads WHERE id = 'finn:1'")));
    }

    [Fact]
    public async Task ShouldStoreFailedRun_WhenProfileIsMissing() {
        AppConfig config = RunFixtures.Config(Path.Combine(dir.Path, "missing.yaml"));

        await Assert.ThrowsAsync<ConfigMissingException>(() => RunAsync(Answering(), [], config, null, TestContext.Current.CancellationToken));

        Assert.Equal(["failed|1"], RunRows());
    }

    [Fact]
    public async Task ShouldStopAsAuth_WhenModelSetupFails() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));

        RunResult result = await RunAsync(null, [], null, null, TestContext.Current.CancellationToken);

        Assert.Equal((RunStatus.Completed, (StopReason?)StopReason.Auth, 1), (result.Status, result.ModelStopped, result.Pending));
    }

    [Fact]
    public async Task ShouldEndCancelled_WhenCancelledDuringApplyChecks() {
        using CancellationTokenSource run = new();
        ads.Upsert(Pending(1, "Utvikler", "Org One AS") with { ApplyUrl = applyUrl });
        FakeSiteHandler site = new(_ => {
            run.Cancel();
            throw new OperationCanceledException(run.Token);
        });

        RunResult result = await RunAsync(Answering(), [], null, site, run.Token);

        Assert.Equal((RunStatus.Cancelled, "cancelled|1"), (result.Status, string.Join(',', RunRows())));
    }

    [Fact]
    public async Task ShouldCiteContextNotes_WhenContextHasAbout() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ProfilesEntry.SaveContext(
            paths.ContextPath(), ProfilesEntry.WithAbout(new(), ContextFixtures.About), NullLogger.Instance);
        FakeModelClient client = Answering();

        await RunAsync(client, [], null, null, TestContext.Current.CancellationToken);

        Assert.Contains("Candidate notes, written or uploaded by the candidate:", client.Prompts[0].System, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldKeepAd_WhenEmployerFeedWasNotRead() {
        ads.Upsert(Pending(1, "Kotlin-utvikler", "Computas") with { Id = "nav:1", Source = "nav" });
        ScriptedSource failing = new(feed.Source, _ => throw new SourceException("feed failed", null));
        AppConfig config = RunFixtures.Config(profilePath, new SourcesConfig { Nav = false, Employers = true, EmployerFeeds = [feed] });

        await RunAsync(null, [failing], config, null, TestContext.Current.CancellationToken);

        Assert.Null(db.Scalar("SELECT apply_status FROM ads WHERE id = 'nav:1'"));
    }

    [Fact]
    public async Task ShouldKeepAdOpen_WhenFeedFailsWithPartialBatch() {
        ads.Upsert(Pending(1, "Kotlin-utvikler", "Computas") with { Id = "teamtailor:computas:1", Source = feed.Source });
        SourceBatch partial = Batch(feed.Source) with { ListedIds = new HashSet<string>() };
        ScriptedSource failing = new(feed.Source, _ => throw new SourceException("feed failed", partial));

        await RunAsync(null, [failing], null, null, TestContext.Current.CancellationToken);

        Assert.Equal(1L, db.Scalar("SELECT active FROM ads WHERE id = 'teamtailor:computas:1'"));
    }

    private static FakeModelClient Answering() => new((_, _) => Task.FromResult(FakeModelClient.Reply(javaReply)));

    private List<string> RunRows() => TestDatabase.Rows(db.Connection, runsQuery);

    private async Task<RunResult> RunAsync(
        IModelClient? client,
        IReadOnlyList<ISource> sources,
        AppConfig? config,
        FakeSiteHandler? site,
        CancellationToken cancellationToken) {
        using HttpClient http = new(site ?? new FakeSiteHandler(_ => FakeSiteHandler.Body("<html>Søk nå</html>")));
        RunsEntry runs = Create(db, paths, http, client, sources, time);
        return await runs.RunAsync(
            new RunRequest(config ?? RunFixtures.Config(profilePath), new RecordingRunEvents(), RunMode.Manual), cancellationToken);
    }
}
