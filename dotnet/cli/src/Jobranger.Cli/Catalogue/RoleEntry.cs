namespace Jobranger.Cli.Catalogue;

internal sealed record RoleEntry(
    string Label,
    IReadOnlyList<string> LinkedInKeywords,
    IReadOnlyList<string> NavStyrk,
    IReadOnlyList<(string Level1, string Level2)> NavCategories,
    IReadOnlyList<string> FinnOccupations,
    IReadOnlyList<string> TitleWords);
