using System.Globalization;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using DigestEntry = Jobranger.Cli.Features.Digest.Digest;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Features.Runs;

internal static class TerminalReport {
    public static IReadOnlyList<string> Lines(
        SqliteConnection connection, AppConfig config, Paths paths, RunRow lastRun, TimeProvider time, IMessages messages) {
        DateTimeOffset now = time.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, time.LocalTimeZone).DateTime);
        DateTimeOffset since = DateTimeOffset.Parse(lastRun.StartedAt, CultureInfo.InvariantCulture);
        string version = PromptSetup.Load(config, paths).Version;
        AdsEntry ads = new(connection, time);
        List<Ad> candidates = Candidates(ads, today, since, version, config.Targets);
        IReadOnlyList<string> candidateIds = [.. candidates.Select(ad => ad.Id)];
        DigestData data = DigestEntry.Build(
            new DigestInput {
                Cards = Cards(candidates, version, config.Targets),
                Hidden = RunConversions.ToHiddenCounts(
                    ReportStage.HiddenCounts(ads, since, version, config.Targets, candidateIds)),
                MaxYears = MatchingEntry.MaxYears(config.Targets.Limits),
                RanAt = now,
            },
            today);
        return Render(data, messages);
    }

    private static List<Ad> Candidates(AdsEntry ads, DateOnly today, DateTimeOffset since, string version, Targets targets) {
        bool IsReportable(Ad ad) => MatchingEntry.IsReportable(RunConversions.ToAdView(ad), version, targets);
        return [
            .. ads.Reportable(today, IsReportable),
            .. ads.KeepReportable(ads.ReportedSince(since), IsReportable).Where(ad => ad.ClosedAt is null),
        ];
    }

    private static List<DigestCard> Cards(IReadOnlyList<Ad> candidates, string version, Targets targets) {
        Dictionary<string, string> urls = candidates.ToDictionary(ad => ad.Id, ad => ad.Url, StringComparer.Ordinal);
        List<DigestCard> cards = [];
        foreach (IReadOnlyList<Ad> group in AdsEntry.GroupDuplicates(candidates)) {
            AdView view = MatchingEntry.PreferredAd([.. group.Select(RunConversions.ToAdView)], version);
            cards.Add(RunConversions.ToDigestCard(view, urls[view.Id], targets));
        }
        return cards;
    }

    private static List<string> Render(DigestData data, IMessages messages) {
        List<string> lines = [];
        AddBlock(lines, messages.DigestApplySoonTitle, data.ApplySoon, messages);
        AddBlock(lines, messages.DigestNewTodayTitle, data.NewToday, messages);
        if (lines.Count == 0) {
            return lines;
        }
        string hiddenLine = DigestEntry.HiddenLine(data.Hidden, data.MaxYears, messages);
        if (hiddenLine.Length > 0) {
            lines.Add(hiddenLine);
        } else {
            lines.RemoveAt(lines.Count - 1);
        }
        return lines;
    }

    private static void AddBlock(List<string> lines, string title, IReadOnlyList<DigestCard> cards, IMessages messages) {
        if (cards.Count == 0) {
            return;
        }
        lines.Add(title);
        lines.AddRange(cards.Select(card => DigestEntry.MatchLine(card, messages)));
        lines.Add(string.Empty);
    }
}
