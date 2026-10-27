using Jobranger.Messaging.Channels;

namespace Jobranger.Relay.Core.Tickets;

public readonly record struct TicketCheck(TicketOutcome Outcome, ChannelId? Channel, string? Grant = null);
