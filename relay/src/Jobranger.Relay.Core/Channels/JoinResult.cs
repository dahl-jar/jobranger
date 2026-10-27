namespace Jobranger.Relay.Core.Channels;

public readonly record struct JoinResult(JoinOutcome Outcome, string? Replaced);
