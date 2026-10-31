using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using static Jobranger.Cli.Tests.Mocks.FakeSiteHandler;
using static Jobranger.Cli.Tests.Mocks.SourceFixtures;

namespace Jobranger.Cli.Tests.Features.Runs;

public sealed class FetchStageTests {
    private static readonly TimeSpan stageLimit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan longGrace = TimeSpan.FromMinutes(1);

    private readonly FakeTimeProvider time = new(RunStarted);
    private readonly RecordingRunEvents events = new();
    private readonly FetchContext context = Context(new FakeSiteHandler(_ => Body("")));

    [Fact]
    public async Task ShouldWritePartialBatch_WhenDeadlinePasses() {
        ScriptedSource stalled = ScriptedSource.Stalled("nav", Batch("nav", "1"));
        RecordingBatchWriter writer = new();
        FakeLogger logger = new();

        Task<FetchTally> running = Stage().RunAsync(
            [stalled], Context(new FakeSiteHandler(_ => Body("")), logger: logger), writer, CancellationToken.None);
        await stalled.Started.Task.WaitAsync(stageLimit, TestContext.Current.CancellationToken);
        time.Advance(SourceDeadline.For("nav"));
        FetchTally tally = await running.WaitAsync(stageLimit, TestContext.Current.CancellationToken);

        Assert.Equal(
            (false, "nav", "nav:1", "source.deadline_passed"),
            (tally.Cancelled, string.Join(',', tally.Failed), writer.Ids, Events(logger)));
    }

    [Fact]
    public async Task ShouldKeepFinnRunning_WhenStandardDeadlinePasses() {
        ScriptedSource stalled = ScriptedSource.Stalled("finn", Batch("finn", "1"));
        RecordingBatchWriter writer = new();

        Task<FetchTally> running = Stage().RunAsync([stalled], context, writer, CancellationToken.None);
        await stalled.Started.Task.WaitAsync(stageLimit, TestContext.Current.CancellationToken);
        time.Advance(SourceDeadline.For("nav"));
        bool stoppedEarly = stalled.Token.IsCancellationRequested;
        time.Advance(SourceDeadline.For("finn") - SourceDeadline.For("nav"));
        FetchTally tally = await running.WaitAsync(stageLimit, TestContext.Current.CancellationToken);

        Assert.Equal((false, "finn", "finn:1"), (stoppedEarly, string.Join(',', tally.Failed), writer.Ids));
    }

    [Fact]
    public async Task ShouldFailSource_WhenSourceIgnoresDeadline() {
        ScriptedSource stuck = new("nav", _ => new TaskCompletionSource<SourceBatch>().Task);
        FakeLogger logger = new();
        RecordingTime clock = new();

        Task<FetchTally> running = new FetchStage(clock, events, longGrace).RunAsync(
            [stuck], Context(new FakeSiteHandler(_ => Body("")), logger: logger), new RecordingBatchWriter(),
            CancellationToken.None);
        await stuck.Started.Task.WaitAsync(stageLimit, TestContext.Current.CancellationToken);
        clock.Advance(SourceDeadline.For("nav"));
        await clock.WaitForAsync(longGrace);
        clock.Advance(longGrace);
        FetchTally tally = await running.WaitAsync(stageLimit, TestContext.Current.CancellationToken);

        Assert.Equal(
            ("nav", "source passed its deadline, kept ads source=nav count=0"),
            (string.Join(',', tally.Failed), logger.LatestRecord.Message));
    }

    [Fact]
    public async Task ShouldWritePartialBatch_WhenRunIsCancelled() {
        using CancellationTokenSource run = new();
        ScriptedSource stalled = ScriptedSource.Stalled("finn", Batch("finn", "1"));
        RecordingBatchWriter writer = new();

        Task<FetchTally> running = Stage().RunAsync([stalled], context, writer, run.Token);
        await stalled.Started.Task.WaitAsync(stageLimit, TestContext.Current.CancellationToken);
        await run.CancelAsync();
        FetchTally tally = await running.WaitAsync(stageLimit, TestContext.Current.CancellationToken);

        Assert.Equal((true, "", "finn:1"), (tally.Cancelled, string.Join(',', tally.Failed), writer.Ids));
    }

    [Fact]
    public async Task ShouldFetchSourcesTogether_WhenOneSourceWaitsForAnother() {
        ScriptedSource nav = ScriptedSource.Done("nav", Batch("nav", "2"));
        ScriptedSource finn = new("finn", async cancellationToken => {
            await nav.Started.Task.WaitAsync(cancellationToken);
            return Batch("finn", "1");
        });

        FetchTally tally = await Stage().RunAsync([finn, nav], context, new RecordingBatchWriter(), CancellationToken.None)
            .WaitAsync(stageLimit, TestContext.Current.CancellationToken);

        Assert.Equal("finn,nav", string.Join(',', tally.SourcesRead.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task ShouldEmitNewCount_WhenSourceFinishes() {
        ScriptedSource nav = ScriptedSource.Done("nav", Batch("nav", "1", "2"));

        await Stage().RunAsync([nav], context, new RecordingBatchWriter(newPerBatch: 2), CancellationToken.None);

        Assert.Equal(
            [new RunEvent(RunStage.SourceStarted, "nav"), new RunEvent(RunStage.SourceDone, "nav", FoundNew: 2)],
            events.Events);
    }

    [Fact]
    public async Task ShouldSumAdCounts_WhenTwoSourcesFinish() {
        ScriptedSource nav = ScriptedSource.Done("nav", Batch("nav", "1", "2"));
        ScriptedSource finn = ScriptedSource.Done("finn", Batch("finn", "3"));

        FetchTally tally = await Stage().RunAsync([nav, finn], context, new RecordingBatchWriter(newPerBatch: 1),
            CancellationToken.None);

        Assert.Equal((3, 2), (tally.AdsRead, tally.NewAds));
    }

    [Fact]
    public async Task ShouldEmitSourceFailed_WhenSourceThrows() {
        ScriptedSource failing = new("nav", _ => throw new SourceException("nav feed failed", null));

        await Stage().RunAsync([failing], context, new RecordingBatchWriter(), CancellationToken.None);

        Assert.Equal(new RunEvent(RunStage.SourceFailed, "nav"), events.Events[^1]);
    }

    [Fact]
    public async Task ShouldStopOtherSources_WhenWriterThrows() {
        ScriptedSource stalled = ScriptedSource.Stalled("finn", Batch("finn", "1"));
        ScriptedSource nav = ScriptedSource.Done("nav", Batch("nav", "1"));

        await Assert.ThrowsAsync<IOException>(() => Stage()
            .RunAsync([stalled, nav], context, new FailingBatchWriter(), CancellationToken.None)
            .WaitAsync(stageLimit, TestContext.Current.CancellationToken));

        Assert.True(stalled.Finished.Task.IsCompleted);
    }

    private FetchStage Stage() => new(time, events);

    private static SourceBatch Batch(string source, params string[] ids) => new() {
        Source = source,
        Listings = [.. ids.Select(id => FinnListing(id) with { Id = $"{source}:{id}", Source = source })],
        SeenIds = new HashSet<string>(ids, StringComparer.Ordinal),
    };
}
