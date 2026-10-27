using System.Text.RegularExpressions;

namespace Jobranger.Cli.Catalogue;

internal sealed class SourcePlan(Regex planWords, Regex generic, Regex developer) {
    public required IReadOnlyList<string> LinkedInKeywords { get; init; }

    public required IReadOnlyList<string> NavStyrk { get; init; }

    public required IReadOnlySet<(string Level1, string Level2)> NavCategories { get; init; }

    public required IReadOnlyList<string> FinnOccupations { get; init; }

    public required IReadOnlySet<string> SkipTitleLevels { get; init; }

    public bool TitleMatches(string jobTitle) =>
        planWords.IsMatch(jobTitle) || (generic.IsMatch(jobTitle) && developer.IsMatch(jobTitle));
}
