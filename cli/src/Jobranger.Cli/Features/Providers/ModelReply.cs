namespace Jobranger.Cli.Features.Providers;

internal sealed record ModelReply(string Text, TokenUsage? Usage, StopKind Stop, decimal? ReportedCostUsd);
