namespace Jobranger.Relay.Core;

public readonly record struct JoinResult(JoinOutcome Outcome, string? Replaced);
