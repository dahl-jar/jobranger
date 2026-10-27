using System.Runtime.Versioning;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Tests.Mocks;
using static Jobranger.Cli.Tests.Mocks.AdFixtures;

namespace Jobranger.Cli.Tests.Features.Runs;

[UnsupportedOSPlatform("windows")]
public sealed class RunStoreTests : IDisposable {
    private readonly TestDatabase db = new();
    private readonly RunStore runs;

    public RunStoreTests() {
        runs = new(db.Connection);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldMarkStaleRunFailed_WhenNextRunStarts() {
        long crashed = runs.Start(Now);

        runs.Start(Later);

        RunRow stale = runs.List(2)[1];
        Assert.Equal((crashed, RunStatus.Failed, "2026-10-10T10:00:00+00:00"), (stale.Id, stale.Status, stale.FinishedAt));
    }

    [Fact]
    public void ShouldReadBackFinishedRun_WhenRunFinished() {
        runs.Start(Now);
        long id = runs.Start(Later);

        runs.Finish(id, new RunSummary(Later.AddMinutes(5), RunStatus.Completed, 7, 5, 3, 2, ["nav"]));

        RunRow last = runs.Last()!;
        Assert.Equal(
            (id, "2026-10-10T10:00:00+00:00", "2026-10-10T10:05:00+00:00", RunStatus.Completed, 7L, 5L, 3L, 2L, "nav"),
            (last.Id, last.StartedAt, last.FinishedAt, last.Status, last.AdsRead, last.NewAds, last.Matches, last.Hidden,
                string.Join('|', last.FailedSources)));
    }

    [Fact]
    public void ShouldReturnNull_WhenNoRunExists() {
        Assert.Null(runs.Last());
    }
}
