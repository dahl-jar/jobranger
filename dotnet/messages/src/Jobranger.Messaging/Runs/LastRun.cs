namespace Jobranger.Messaging.Runs;

public sealed record LastRun(long RunId, string FinishedAt, long AdsRead, long Matches, double CostUsd);
