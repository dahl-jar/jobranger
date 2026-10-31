namespace Jobranger.Cli.Features.Models;

internal sealed record ModelSpec(
    decimal Input, decimal Output, decimal CacheRead, decimal CacheWrite5m, decimal CacheWrite1h, bool SupportsEffort);
