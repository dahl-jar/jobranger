namespace Jobranger.Cli.Features.Budget;

internal sealed record UsageCounts(
    long Calls = 0,
    long InputTokens = 0,
    long OutputTokens = 0,
    double CostUsd = 0,
    long AdsRead = 0,
    long AdsChecked = 0);
