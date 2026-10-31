namespace Jobranger.Cli.Features.Sources;

internal sealed record FinnSearchPage(
    IReadOnlyList<SourceListing> Listings,
    int MatchCount,
    IReadOnlyDictionary<string, string> JobTitles,
    int LastPage);
