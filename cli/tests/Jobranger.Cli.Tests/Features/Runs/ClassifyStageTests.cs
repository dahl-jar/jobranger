using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using static Jobranger.Cli.Tests.Mocks.RunFixtures;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Runs;

[UnsupportedOSPlatform("windows")]
public sealed class ClassifyStageTests : IDisposable {
    private const string version = MatchingFixtures.Version;
    private const string labelsQuery = "SELECT label FROM ads ORDER BY id";

    private static readonly ModelPrices prices = new(1m, 5m, 0.1m, 1.25m, 2m);
    private static readonly PromptSetup prompt =
        new("system text", version, MatchingFixtures.EvidenceIds, MatchingFixtures.EducationIds);

    private readonly TestDatabase db = new();
    private readonly AdsEntry ads;
    private readonly RecordingRunEvents events = new();
    private readonly ClassifyStage stage;

    public ClassifyStageTests() {
        ads = new AdsEntry(db.Connection, AdFixtures.Clock());
        stage = new ClassifyStage(ads, new BudgetEntry(db.Connection), events, NullLogger.Instance);
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
    [InlineData("Server", "ProviderDown")]
    public async Task ShouldStopWithAdsPending_WhenProviderFails(string kind, string expected) {
        ads.Upsert(Pending(1, "Utvikler", "Org One AS"));
        ads.Upsert(Pending(2, "Tester", "Org Two AS"));
        FakeModelClient client = new((_, _) => throw new ApiException(Enum.Parse<ApiErrorKind>(kind), "model call failed"));
        FakeLogger logger = new();
        ClassifyStage logged = new(ads, new BudgetEntry(db.Connection), events, logger);

        ClassifyOutcome outcome = await logged.RunAsync(Input(client), CancellationToken.None);

        Assert.Equal(
            (new ClassifyOutcome(2, Enum.Parse<StopReason>(expected)), "classify.provider.failed"),
            (outcome, logger.LatestRecord.Id.Name));
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
        decimal? cost = reported is null ? null : decimal.Parse(reported, System.Globalization.CultureInfo.InvariantCulture);
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

        ads.RefreshAssessments(version);

        Assert.Equal("error", Labels());
    }

    private static ClassifyInput Input(IModelClient? client) => new(
        client is null ? null : new RunModel(client, new JsonObject(), prices),
        prompt,
        MatchingFixtures.Narrow,
        AdFixtures.Today);

    private string Labels() => string.Join(',', TestDatabase.Rows(db.Connection, labelsQuery));
}
