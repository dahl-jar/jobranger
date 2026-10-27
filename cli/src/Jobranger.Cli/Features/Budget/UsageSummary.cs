namespace Jobranger.Cli.Features.Budget;

internal sealed record UsageSummary(
    decimal MonthUsd,
    long MonthTokens,
    decimal TodayUsd,
    long TodayTokens,
    decimal AvgUsd,
    decimal PeakUsd,
    decimal? LimitUsd,
    decimal? UsedPct,
    DateOnly ResetsOn,
    IReadOnlyList<UsageDay> Days);
