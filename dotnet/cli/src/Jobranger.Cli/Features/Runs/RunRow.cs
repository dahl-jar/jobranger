namespace Jobranger.Cli.Features.Runs;

internal sealed record RunRow(
    long Id,
    string StartedAt,
    string? FinishedAt,
    RunStatus Status,
    long AdsRead,
    long NewAds,
    long Matches,
    long Hidden,
    IReadOnlyList<string> FailedSources);
