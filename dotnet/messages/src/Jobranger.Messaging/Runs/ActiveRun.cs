namespace Jobranger.Messaging.Runs;

public sealed record ActiveRun(long RunId, string Mode, string StartedAt, string? ResultsBy);
