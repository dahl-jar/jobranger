using Jobranger.Messaging.Channels;

namespace Jobranger.Relay.Core.Pairing;

public readonly record struct ClaimResult(ClaimOutcome Outcome, ChannelId? Channel);
