namespace Jobranger.Cli.Features.Runs;

internal sealed record RunSummary(
    DateTimeOffset FinishedAt,
    RunStatus Status,
    long AdsRead,
    long NewAds,
    long Matches,
    long Hidden,
    IReadOnlyList<string> FailedSources);
