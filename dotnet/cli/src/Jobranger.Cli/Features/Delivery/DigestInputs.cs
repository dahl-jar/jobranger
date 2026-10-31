using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Runs;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;

namespace Jobranger.Cli.Features.Delivery;

[UnsupportedOSPlatform("windows")]
internal static class DigestInputs {
    public static DigestInput From(
        AdsEntry ads, IReadOnlyList<RunRow> recentRuns, RunResult result, AppConfig config, DateTimeOffset ranAt) => new() {
            Cards = result.Version is string version ? Cards(ads, result, config.Targets, version) : [],
            CarriedIds = result.CarriedIds,
            Hidden = RunsEntry.ToHiddenCounts(result.HiddenCounts),
            NewAds = result.NewAds,
            SourcesRead = result.SourcesRead,
            FailedSources = result.FailedSources,
            ClosedSinceLast = ClosedSinceLast(ads, recentRuns, result.RunId),
            BudgetPaused = result.BudgetPaused,
            Pending = result.Pending,
            ModelStopped = result.ModelStopped switch {
                StopReason.Auth => DigestStop.Auth,
                StopReason.ProviderDown => DigestStop.ProviderDown,
                _ => null,
            },
            MaxYears = MatchingEntry.MaxYears(config.Targets.Limits),
            RanAt = ranAt,
        };

    private static List<DigestCard> Cards(AdsEntry ads, RunResult result, Targets targets, string version) {
        IReadOnlyList<Ad> kept = ads.KeepReportable(
            ads.ById(result.ReportedIds),
            ad => MatchingEntry.IsReportable(RunsEntry.ToAdView(ad), version, targets));
        return [.. AdsEntry.GroupDuplicates(kept).Select(group => Card(group, targets, version))];
    }

    private static DigestCard Card(IReadOnlyList<Ad> group, Targets targets, string version) {
        AdView view = MatchingEntry.PreferredAd([.. group.Select(RunsEntry.ToAdView)], version);
        return RunsEntry.ToDigestCard(view, group.First(ad => ad.Id == view.Id).Url, targets);
    }

    private static int ClosedSinceLast(AdsEntry ads, IReadOnlyList<RunRow> recentRuns, long? runId) =>
        recentRuns.FirstOrDefault(run => run.Id != runId) is RunRow previous ? (int)ads.ClosedSince(previous.StartedAt) : 0;
}
