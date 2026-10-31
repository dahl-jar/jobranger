using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Matching;

internal static class AdPreference {
    private const string expiredStatus = "expired";

    private static readonly string[] directSourcePrefixes = ["ashby:", "teamtailor:"];
    private static readonly string[] titleLevelPrecedence = [TitleLevels.Lead, TitleLevels.Senior];

    public static AdView Preferred(IReadOnlyList<AdView> group, string version) {
        AdView selected = group.MaxBy(ad => (IsDirectSource(ad), DescriptionLength(ad)))!;
        AdView classified = group.MaxBy(ad => (
            AdRules.HasCurrentMatch(ad, version),
            HasBlockedMatch(ad),
            ad.Technologies.Count + ad.Requirements.Count,
            !string.IsNullOrEmpty(ad.DisplayTitle),
            DescriptionLength(ad)))!;
        AdView content = group.MaxBy(DescriptionLength)!;
        bool expired = group.Any(member => member.ApplyStatus == expiredStatus);
        return selected with {
            Description = content.Description,
            Label = TitleLevel(group) ?? classified.Label,
            Summary = classified.Summary,
            RequiresMaster = group.Any(member => member.RequiresMaster),
            IsProgrammingJob = classified.IsProgrammingJob,
            DisplayTitle = classified.DisplayTitle,
            RoleType = classified.RoleType,
            Technologies = classified.Technologies,
            Requirements = classified.Requirements,
            Match = classified.Match,
            Deadline = Earliest(group.Select(member => member.Deadline)),
            Published = Earliest(group.Select(member => member.Published)),
            Republished = group.Any(member => member.Republished),
            ApplyStatus = expired ? expiredStatus : selected.ApplyStatus,
            ApplyUrl = string.IsNullOrEmpty(selected.ApplyUrl)
                ? group.Select(member => member.ApplyUrl).FirstOrDefault(url => !string.IsNullOrEmpty(url))
                : selected.ApplyUrl,
        };
    }

    private static string? TitleLevel(IReadOnlyList<AdView> group) =>
        titleLevelPrecedence.FirstOrDefault(level => group.Any(member => member.Label == level));

    private static bool IsDirectSource(AdView ad) =>
        directSourcePrefixes.Any(prefix => ad.Source.StartsWith(prefix, StringComparison.Ordinal));

    private static int DescriptionLength(AdView ad) => CodePoints.Count(ad.Description ?? string.Empty);

    private static bool HasBlockedMatch(AdView ad) =>
        ad.Match is Match match && (match.Status == Fit.Unsuitable || match.Blockers.Count > 0 || match.IsLowCode);

    private static string? Earliest(IEnumerable<string?> values) =>
        values.Where(value => !string.IsNullOrEmpty(value)).Order(StringComparer.Ordinal).FirstOrDefault();
}
