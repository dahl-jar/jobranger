namespace Jobranger.Cli.Features.Budget;

internal sealed record SpendRecord(DateOnly Day, TokenCounts Tokens, decimal CostUsd);
