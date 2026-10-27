namespace Jobranger.Cli.Features.Budget;

internal sealed record ModelPrices(
    decimal Input, decimal Output, decimal CacheRead, decimal CacheWrite5m, decimal CacheWrite1h);
