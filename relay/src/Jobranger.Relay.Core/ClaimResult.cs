namespace Jobranger.Relay.Core;

public readonly record struct ClaimResult(ClaimOutcome Outcome, ChannelId? Channel);
