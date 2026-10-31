namespace Jobranger.Cli.Features.Runs;

internal sealed record FetchTally(
    int AdsRead,
    int NewAds,
    IReadOnlyList<string> Failed,
    IReadOnlyList<string> SourcesRead,
    bool Cancelled) {
    public static readonly FetchTally Empty = new(0, 0, [], [], false);
}
