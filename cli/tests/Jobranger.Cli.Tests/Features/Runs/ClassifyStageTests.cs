using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using static Jobranger.Cli.Tests.Mocks.RunFixtures;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Runs;

[UnsupportedOSPlatform("windows")]
public sealed class ClassifyStageTests : IDisposable {
    private const string version = MatchingFixtures.Version;
    private const string labelsQuery = "SELECT label FROM ads ORDER BY id";
    private const string assessedQuery = "SELECT label || '|' || assessment_version FROM ads";
    private const string busyAd = "finn:2";

    private static readonly ModelPrices prices = new(1m, 5m, 0.1m, 1.25m, 2m);
    private static readonly PromptSetup prompt =
        new("system text", version, MatchingFixtures.EvidenceIds, MatchingFixtures.EducationIds);

    private static readonly TimeSpan busyPause = TimeSpan.FromSeconds(30);

    private readonly TestDatabase db = new();
    private readonly FakeTimeProvider time = AdFixtures.Clock();
    private readonly AdsEntry ads;
    private readonly RecordingRunEvents events = new();
    private readonly ClassifyStage stage;

    public ClassifyStageTests() {
        ads = new AdsEntry(db.Connection, time);
        stage = StageWith(events, NullLogger.Instance);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task ShouldStoreClassification_WhenNextCallIsStillRunning() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.Upsert(Pending(2, "Tester", "Org Two AS"));
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeModelClient client = null!;
        client = new FakeModelClient(async (_, _) => {
            if (client.Calls == 2) {
                await gate.Task;
            }
            return FakeModelClient.Reply(JavaReply);
        });

        Task<ClassifyOutcome> running = stage.RunAsync(Input(client), CancellationToken.None);
        await TestWaits.UntilAsync(() => client.Calls == 2);
        string? firstLabel = (string?)db.Scalar("SELECT label FROM ads WHERE id = 'finn:1'");
        gate.SetResult();
        await running;

        Assert.Equal("junior", firstLabel);
    }

    [Fact]
    public async Task ShouldClassifyWholeGroup_WhenDuplicatesArePending() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.Upsert(Pending(2, "Utvikler", "Org One AS") with { Id = "nav:2", Source = "nav" });
        FakeModelClient client = FakeModelClient.Answering(JavaReply);

        await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal((1, "junior,junior"), (client.Calls, Labels()));
    }

    [Fact]
    public async Task ShouldReuseStoredMatch_WhenGroupHasCurrentMatch() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.SetClassification("finn:1", AdFixtures.Classification with {
            Assessment = MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch, []),
        }, version);
        ads.Upsert(Pending(2, "Utvikler", "Org One AS") with { Id = "nav:2", Source = "nav" });
        FakeModelClient client = FakeModelClient.Answering(JavaReply);

        await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal((0, "junior,junior"), (client.Calls, Labels()));
    }

    [Theory]
    [InlineData("error", 1, version)]
    [InlineData("junior", null, version)]
    [InlineData("junior", 1, "old-version")]
    public async Task ShouldCallModel_WhenStoredDuplicateIsNotReusable(string label, int? isProgrammingJob, string matchVersion) {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.SetClassification("finn:1", AdFixtures.Classification with {
            Label = label,
            Assessment = MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch with { Version = matchVersion }, []),
        }, version);
        db.Execute("UPDATE ads SET is_programming_job = $0 WHERE id = 'finn:1'", isProgrammingJob);
        ads.Upsert(Pending(2, "Utvikler", "Org One AS") with { Id = "nav:2", Source = "nav" });
        FakeModelClient client = FakeModelClient.Answering(JavaReply);

        await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal(1, client.Calls);
    }

    [Theory]
    [InlineData("Auth", "Auth")]
    [InlineData("BadRequest", "ProviderDown")]
    [InlineData("NotFound", "ProviderDown")]
    public async Task ShouldStopWithAdsPending_WhenProviderRefusesCall(string kind, string expected) {
        UpsertAds(2);
        FakeModelClient client = new((_, _) => throw new ApiException(Enum.Parse<ApiErrorKind>(kind), "model call failed"));
        FakeLogger logger = new();

        ClassifyOutcome outcome = await StageWith(events, logger).RunAsync(Input(client), CancellationToken.None);

        Assert.Equal(
            (new ClassifyOutcome(2, Enum.Parse<StopReason>(expected)), 1, "classify.provider.failed"),
            (outcome, client.Calls, logger.LatestRecord.Id.Name));
    }

    [Fact]
    public async Task ShouldStopAsAuth_WhenModelIsMissing() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));

        ClassifyOutcome outcome = await stage.RunAsync(Input(null), CancellationToken.None);

        Assert.Equal(new ClassifyOutcome(1, StopReason.Auth), outcome);
    }

    [Fact]
    public async Task ShouldBumpAttempts_WhenReplyIsTruncated() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        FakeModelClient client = new((_, _) => Task.FromResult(FakeModelClient.Reply(JavaReply, StopKind.Truncated)));

        await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal(["1|NULL"], TestDatabase.Rows(db.Connection, "SELECT label_attempts, label FROM ads"));
    }

    [Fact]
    public async Task ShouldLabelError_WhenAttemptsRunOut() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        db.Execute("UPDATE ads SET label_attempts = 3, assessment_version = $0", version);

        await stage.RunAsync(Input(null), CancellationToken.None);

        Assert.Equal("error", Labels());
    }

    [Theory]
    [InlineData("0.5", "0.500000")]
    [InlineData(null, "0.000200")]
    public async Task ShouldRecordCost_WhenReplyArrives(string? reported, string expected) {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        decimal? cost = reported is null ? null : decimal.Parse(reported, CultureInfo.InvariantCulture);
        FakeModelClient client = new((_, _) => Task.FromResult(FakeModelClient.Reply(JavaReply, cost: cost)));

        await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal([expected], TestDatabase.Rows(db.Connection, "SELECT printf('%.6f', cost_usd) FROM usage_days"));
    }

    [Fact]
    public async Task ShouldStopAsCancelled_WhenRunIsCancelled() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.Upsert(Pending(2, "Tester", "Org Two AS"));
        using CancellationTokenSource run = new();
        FakeModelClient client = new(async (_, token) => {
            await run.CancelAsync();
            token.ThrowIfCancellationRequested();
            return FakeModelClient.Reply(JavaReply);
        });

        ClassifyOutcome outcome = await stage.RunAsync(Input(client), run.Token);

        Assert.Equal(new ClassifyOutcome(2, StopReason.Cancelled), outcome);
    }

    [Fact]
    public async Task ShouldSkipNextCall_WhenRunIsCancelledBetweenCalls() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.Upsert(Pending(2, "Tester", "Org Two AS"));
        using CancellationTokenSource run = new();
        FakeModelClient client = new(async (_, _) => {
            await run.CancelAsync();
            return FakeModelClient.Reply(JavaReply);
        });

        ClassifyOutcome outcome = await stage.RunAsync(Input(client), run.Token);

        Assert.Equal((new ClassifyOutcome(1, StopReason.Cancelled), 1), (outcome, client.Calls));
    }

    [Fact]
    public async Task ShouldEmitProgress_WhenReplyArrives() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.Upsert(Pending(2, "Tester", "Org Two AS"));

        await stage.RunAsync(Input(FakeModelClient.Answering(JavaReply)), CancellationToken.None);

        Assert.Equal(
            [new RunEvent(RunStage.Model, Checked: 1, Total: 2), new RunEvent(RunStage.Model, Checked: 2, Total: 2)],
            events.Events);
    }

    [Fact]
    public async Task ShouldKeepErrorLabel_WhenVersionIsRefreshed() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        db.Execute("UPDATE ads SET label_attempts = 3, assessment_version = $0", version);
        await stage.RunAsync(Input(null), CancellationToken.None);

        ads.ResetUnassessed(version);

        Assert.Equal("error", Labels());
    }

    [Fact]
    public async Task ShouldSendFirstCallAlone_WhenRunStarts() {
        UpsertAds(3);
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeModelClient client = null!;
        client = new FakeModelClient(async (_, _) => {
            if (client.Calls == 1) {
                await gate.Task;
            }
            return FakeModelClient.Reply(JavaReply);
        });

        Task<ClassifyOutcome> running = stage.RunAsync(Input(client), CancellationToken.None);
        await TestWaits.UntilAsync(() => client.Calls >= 1);
        int beforeReply = client.Calls;
        gate.SetResult();
        await running;

        Assert.Equal((1, 3), (beforeReply, client.Calls));
    }

    [Fact]
    public async Task ShouldKeepFourCallsInFlight_WhenTenAdsWait() {
        UpsertAds(10);
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0;
        int peak = 0;
        FakeModelClient client = null!;
        client = new FakeModelClient(async (_, _) => {
            if (client.Calls > 1) {
                peak = Math.Max(peak, Interlocked.Increment(ref active));
                await gate.Task;
                Interlocked.Decrement(ref active);
            }
            return FakeModelClient.Reply(JavaReply);
        });

        Task<ClassifyOutcome> running = stage.RunAsync(Input(client), CancellationToken.None);
        await TestWaits.UntilAsync(() => client.Calls >= 5);
        gate.SetResult();
        await running;

        Assert.Equal((4, 10), (peak, client.Calls));
    }

    [Fact]
    public async Task ShouldStopNewCalls_WhenBudgetRunsOutMidRun() {
        UpsertAds(10);
        FakeModelClient client = new((_, _) => Task.FromResult(FakeModelClient.Reply(JavaReply, cost: 1m)));

        ClassifyOutcome outcome = await stage.RunAsync(Input(client, limit: 2.50m), CancellationToken.None);

        Assert.Equal((2, new ClassifyOutcome(8, StopReason.Budget)), (client.Calls, outcome));
    }

    [Fact]
    public async Task ShouldWaitForRunningCalls_WhenLimitIsNearlyReached() {
        UpsertAds(4);
        FakeModelClient client = null!;
        client = new FakeModelClient((_, _) =>
            Task.FromResult(FakeModelClient.Reply(JavaReply, cost: client.Calls == 1 ? 1m : 0.10m)));

        ClassifyOutcome outcome = await stage.RunAsync(Input(client, limit: 3.50m), CancellationToken.None);

        Assert.Equal((4, new ClassifyOutcome(0, null)), (client.Calls, outcome));
    }

    [Theory]
    [InlineData("RateLimit")]
    [InlineData("Overloaded")]
    public async Task ShouldPauseNextCall_WhenProviderIsBusy(string kind) {
        UpsertAds(2);
        ConcurrentQueue<DateTimeOffset> started = new();
        bool refused = false;
        FakeModelClient client = new((prompt, _) => {
            started.Enqueue(time.GetUtcNow());
            if (FakeModelClient.AdId(prompt) == busyAd && !refused) {
                refused = true;
                throw new ApiException(Enum.Parse<ApiErrorKind>(kind), "busy", busyPause);
            }
            return Task.FromResult(FakeModelClient.Reply(JavaReply));
        });

        await ApiClock.RunAsync(time, stage.RunAsync(Input(client), CancellationToken.None));

        DateTimeOffset[] times = [.. started];
        Assert.True(times[2] - times[1] >= busyPause, $"third call started {times[2] - times[1]} after the refused one");
    }

    [Fact]
    public async Task ShouldStopAsProviderDown_WhenThreeCallsFailInARow() {
        UpsertAds(2);
        FakeModelClient client = new((_, _) => throw new ApiException(ApiErrorKind.Server, "model call failed"));

        ClassifyOutcome outcome = await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal((new ClassifyOutcome(2, StopReason.ProviderDown), 3), (outcome, client.Calls));
    }

    [Fact]
    public async Task ShouldFinishClassifying_WhenFailuresAreNotInARow() {
        UpsertAds(2);
        int[] failingCalls = [1, 2, 4, 5];
        FakeModelClient client = null!;
        client = new FakeModelClient((_, _) => failingCalls.Contains(client.Calls)
            ? throw new ApiException(ApiErrorKind.Server, "model call failed")
            : Task.FromResult(FakeModelClient.Reply(JavaReply)));

        ClassifyOutcome outcome = await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal((new ClassifyOutcome(0, null), "junior,junior"), (outcome, Labels()));
    }

    [Fact]
    public async Task ShouldStoreRunningCall_WhenAnotherCallStopsClassification() {
        UpsertAds(3);
        TaskCompletionSource authSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeLogger logger = new(_ => authSeen.TrySetResult());
        FakeModelClient client = new(async (prompt, _) => {
            string adId = FakeModelClient.AdId(prompt);
            if (adId == "finn:2") {
                await authSeen.Task;
            }
            if (adId == "finn:3") {
                throw new ApiException(ApiErrorKind.Auth, "model call failed");
            }
            return FakeModelClient.Reply(JavaReply);
        });

        ClassifyOutcome outcome = await StageWith(events, logger).RunAsync(Input(client), CancellationToken.None);

        Assert.Equal(
            (new ClassifyOutcome(1, StopReason.Auth), "junior"),
            (outcome, (string?)db.Scalar("SELECT label FROM ads WHERE id = 'finn:2'")));
    }

    [Fact]
    public async Task ShouldCancelRunningCalls_WhenStageThrows() {
        UpsertAds(5);
        string[] answered = ["finn:1", "finn:2"];
        ConcurrentQueue<CancellationToken> tokens = new();
        FakeModelClient client = new(async (prompt, token) => {
            if (!answered.Contains(FakeModelClient.AdId(prompt))) {
                tokens.Enqueue(token);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return FakeModelClient.Reply(JavaReply);
        });

        Task<ClassifyOutcome> running = StageWith(new FailingRunEvents(2), NullLogger.Instance)
            .RunAsync(Input(client), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => running.WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken));
        Assert.Equal([true, true, true], tokens.Select(token => token.IsCancellationRequested));
    }

    [Fact]
    public async Task ShouldReclassifyAd_WhenAssessmentIsOutdated() {
        UpsertOutdated();
        FakeModelClient client = FakeModelClient.Answering(JavaReply);

        await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal((1, "junior|" + version), (client.Calls, (string?)db.Scalar(assessedQuery)));
    }

    [Fact]
    public async Task ShouldKeepPreviousAssessment_WhenProviderFails() {
        UpsertOutdated();
        FakeModelClient client = new((_, _) => throw new ApiException(ApiErrorKind.Auth, "model call failed"));

        await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal("junior|" + AdFixtures.OutdatedVersion, db.Scalar(assessedQuery));
    }

    [Fact]
    public async Task ShouldClassifyNewAdsFirst_WhenOutdatedAdsWait() {
        UpsertOutdated();
        ads.Upsert(Pending(2, "Tester", "Org Two AS"));
        FakeModelClient client = FakeModelClient.Answering(JavaReply);

        await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal("finn:2", FakeModelClient.AdId(client.Prompts[0]));
    }

    [Fact]
    public async Task ShouldLabelError_WhenOutdatedAdRunsOutOfAttempts() {
        UpsertOutdated();
        db.Execute("UPDATE ads SET label_attempts = 2");
        FakeModelClient client = new((_, _) => Task.FromResult(FakeModelClient.Reply(JavaReply, StopKind.Truncated)));

        await stage.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal("error|" + version, db.Scalar(assessedQuery));
    }

    [Fact]
    public async Task ShouldCountNoDroppedAd_WhenReplyIsTruncated() {
        UpsertAds(1);
        FakeModelClient client = new((_, _) => Task.FromResult(FakeModelClient.Reply(JavaReply, StopKind.Truncated)));
        PromptSetup educationOnly = prompt with { Evidence = MatchingFixtures.EducationIds };

        ClassifyOutcome outcome = await stage.RunAsync(Input(client, setup: educationOnly), CancellationToken.None);

        Assert.Equal(0, outcome.UnknownEvidenceDropped);
    }

    private static ClassifyInput Input(IModelClient? client, decimal? limit = null, PromptSetup? setup = null) {
        AppConfig config = RunFixtures.Config("profile.yaml");
        return new ClassifyInput(
            client is null ? null : new RunModel(client, new JsonObject(), prices),
            setup ?? prompt,
            config with { Model = config.Model with { MonthlyLimitUsd = limit } },
            AdFixtures.Today);
    }

    private ClassifyStage StageWith(IRunEvents sink, ILogger logger) =>
        new(ads, new BudgetEntry(db.Connection), time, sink, logger);

    private void UpsertAds(int count) {
        for (int index = 1; index <= count; index++) {
            ads.Upsert(Pending(index, "Utvikler", "Org" + index.ToString(CultureInfo.InvariantCulture) + " AS"));
        }
    }

    private void UpsertOutdated() {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.SetClassification("finn:1", AdFixtures.Classification, AdFixtures.OutdatedVersion);
    }

    private string Labels() => string.Join(',', TestDatabase.Rows(db.Connection, labelsQuery));
}
