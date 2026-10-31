using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Runs;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using CatalogueEntry = Jobranger.Cli.Catalogue.Catalogue;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;

namespace Jobranger.Cli.Features.Delivery;

[UnsupportedOSPlatform("windows")]
internal static class DeliveryCards {
    public static DigestInput Input(
        AdsEntry ads, IReadOnlyList<RunRow> recentRuns, RunResult result, AppConfig config, DateTimeOffset ranAt) => new() {
            Cards = result.Version is string version ? Cards(ads, result, config.Targets, version) : [],
            CarriedIds = result.CarriedIds,
            Hidden = [.. result.HiddenCounts.Select(hidden =>
                new HiddenCount(Enum.Parse<HiddenGroup>(hidden.Key.ToString()), hidden.Value))],
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
        return new DigestCard {
            Id = view.Id,
            Source = view.Source,
            Title = view.Title,
            DisplayTitle = view.DisplayTitle,
            Company = view.Company,
            Location = view.Location,
            Deadline = view.Deadline,
            DeadlineDate = MatchingEntry.ParseDate(view.Deadline),
            Url = group.First(ad => ad.Id == view.Id).Url,
            Summary = view.Summary,
            RoleLabel = CatalogueEntry.Load().RoleLabel(view.RoleType),
            Fit = Enum.Parse<DigestFit>(MatchingEntry.DisplayFit(view, targets).ToString()),
            IsStrongMatch = MatchingEntry.IsStrongMatch(view),
            ApplyExpired = view.ApplyStatus == WireNames.Of(ApplyStatus.Expired),
        };
    }

    private static int ClosedSinceLast(AdsEntry ads, IReadOnlyList<RunRow> recentRuns, long? runId) =>
        recentRuns.FirstOrDefault(run => run.Id != runId) is RunRow previous ? (int)ads.ClosedSince(previous.StartedAt) : 0;
}
