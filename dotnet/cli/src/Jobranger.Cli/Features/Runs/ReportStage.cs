using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Microsoft.Extensions.Logging;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using SourcesEntry = Jobranger.Cli.Features.Sources.Sources;

namespace Jobranger.Cli.Features.Runs;

internal sealed class ReportStage(AdsEntry ads, HttpClient http, TimeProvider time, IRunEvents events, ILogger logger) {
    private static readonly TimeSpan reportedLookback = TimeSpan.FromDays(90);
    private static readonly TimeSpan carryWindow = TimeSpan.FromDays(7);
    private static readonly string expiredStatus = WireNames.Of(ApplyStatus.Expired);
    private static readonly string activeStatus = WireNames.Of(ApplyStatus.Active);

    public static Dictionary<HiddenKind, int> HiddenCounts(
        AdsEntry ads, DateTimeOffset since, string version, Targets targets, IReadOnlyList<string> reported) {
        HashSet<string> reportedIds = [.. reported];
        Dictionary<HiddenKind, int> kinds = [];
        foreach (Ad ad in ads.NewSince(since)) {
            if (reportedIds.Contains(ad.Id) || ad.Label is null) {
                continue;
            }
            if (MatchingEntry.HiddenReason(RunConversions.ToAdView(ad), version, targets) is HiddenKind kind) {
                kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            }
        }
        return kinds;
    }

    public async Task<ReportOutcome> RunAsync(ReportInput input, CancellationToken cancellationToken) {
        DateTimeOffset now = time.GetUtcNow();
        events.Emit(new RunEvent(RunStage.ApplyChecks));
        IReadOnlyDictionary<ApplyStatus, int> counts = await ads
            .CheckApplyLinksAsync(http, now, input.Today, input.Targets, cancellationToken)
            .ConfigureAwait(false);
        int checkedLinks = counts.Values.Sum();
        int expired = counts.GetValueOrDefault(ApplyStatus.Expired);
        int unknown = counts.GetValueOrDefault(ApplyStatus.Unknown);
        int skipped = counts.GetValueOrDefault(ApplyStatus.Skipped);
        int blocked = counts.GetValueOrDefault(ApplyStatus.Blocked);
        LogEvents.ApplyChecksDone(logger, checkedLinks, expired, unknown, skipped, blocked);
        if (input.ReadFeeds.Count > 0) {
            VetoMissingListings(input, now);
        }
        ads.ClosePastDeadline(input.Today);
        events.Emit(new RunEvent(RunStage.Report));
        IReadOnlyList<Ad> reportable = ads.Reportable(input.Today, ad =>
            MatchingEntry.IsReportable(RunConversions.ToAdView(ad), input.Version, input.Targets));
        (IReadOnlyList<Ad> remaining, IReadOnlyList<string> covered) =
            AdsEntry.SplitReported(reportable, ads.ReportedSince(now - reportedLookback));
        (List<string> reported, List<string> carried, List<string> silent) = PickDue(input, remaining, covered, now);
        return new ReportOutcome(
            reported, carried, silent, HiddenCounts(ads, input.StartedAt, input.Version, input.Targets, reported));
    }

    private static string MatchedAt(Ad ad) {
        string seen = ad.FirstSeen ?? string.Empty;
        string classified = ad.FirstClassifiedAt ?? string.Empty;
        return string.CompareOrdinal(seen, classified) >= 0 ? seen : classified;
    }

    private static bool IsVetoed(
        Ad ad, EmployerFeed feed, IReadOnlyList<string> feedTitles, IReadOnlyDictionary<string, string> jobTitles) {
        if (!SourcesEntry.ImportedSources.Contains(ad.Source)
            || ad.ApplyStatus == expiredStatus
            || ad.ApplyStatus == activeStatus
            || !AdsEntry.CompaniesMatch(ad.Company, feed.Company)) {
            return false;
        }
        string? listedTitle = ad.Source == SourcesEntry.Finn ? jobTitles.GetValueOrDefault(ad.Id) : ad.Title;
        return listedTitle is not null && !feedTitles.Any(title => AdsEntry.SameTitle(title, listedTitle));
    }

    private void VetoMissingListings(ReportInput input, DateTimeOffset now) {
        IReadOnlyList<Ad> active = ads.Active();
        IReadOnlyList<IReadOnlyList<Ad>> groups = AdsEntry.GroupDuplicates(active);
        foreach (EmployerFeed feed in input.ReadFeeds) {
            List<string> feedTitles = [.. active.Where(ad => ad.Source == feed.Source).Select(ad => ad.Title)];
            foreach (IReadOnlyList<Ad> group in groups) {
                if (group.Any(ad => ad.Source == feed.Source)) {
                    continue;
                }
                foreach (Ad ad in group.Where(ad => IsVetoed(ad, feed, feedTitles, input.JobTitles))) {
                    ads.SetApplyStatus(ad.Id, ApplyStatus.Expired, now);
                    LogEvents.ApplyVetoed(logger, ad.Id, feed.Source);
                }
            }
        }
    }

    private (List<string> Reported, List<string> Carried, List<string> Silent) PickDue(
        ReportInput input, IReadOnlyList<Ad> remaining, IReadOnlyList<string> covered, DateTimeOffset now) {
        IReadOnlySet<string> shown = ads.ShownAnyway();
        string carryFrom = StoredTime.Format(now - carryWindow);
        string startedAt = StoredTime.Format(input.StartedAt);
        List<Ad> due = [.. remaining.Where(ad =>
            !MatchingEntry.IsStale(RunConversions.ToAdView(ad), input.Today)
            && (string.CompareOrdinal(MatchedAt(ad), carryFrom) >= 0 || shown.Contains(ad.Id)))];
        HashSet<string> dueIds = [.. due.Select(ad => ad.Id)];
        List<string> carried = [.. due.Where(ad => string.CompareOrdinal(MatchedAt(ad), startedAt) < 0).Select(ad => ad.Id)];
        List<string> silent = [.. covered, .. remaining.Where(ad => !dueIds.Contains(ad.Id)).Select(ad => ad.Id)];
        return ([.. due.Select(ad => ad.Id)], carried, silent);
    }
}
