namespace Jobranger.Cli.Features.Providers;

internal sealed record TokenUsage(long Input, long CacheWrite5m, long CacheWrite1h, long CacheRead, long Output);
