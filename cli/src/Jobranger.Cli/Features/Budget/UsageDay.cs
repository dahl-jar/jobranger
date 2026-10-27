namespace Jobranger.Cli.Features.Budget;

internal sealed record UsageDay(
    string Day,
    long Calls,
    long InputTokens,
    long OutputTokens,
    double CostUsd,
    long AdsRead,
    long AdsChecked);
