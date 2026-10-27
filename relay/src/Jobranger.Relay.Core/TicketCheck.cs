namespace Jobranger.Relay.Core;

public readonly record struct TicketCheck(TicketOutcome Outcome, ChannelId? Channel, string? Grant = null);
