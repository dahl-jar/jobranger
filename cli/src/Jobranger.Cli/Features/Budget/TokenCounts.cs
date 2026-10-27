namespace Jobranger.Cli.Features.Budget;

internal sealed record TokenCounts(long Input, long CacheWrite5m, long CacheWrite1h, long CacheRead, long Output);
