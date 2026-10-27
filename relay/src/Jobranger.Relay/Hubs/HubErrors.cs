using Jobranger.Relay.Core;

namespace Jobranger.Relay.Hubs;

internal static class HubErrors
{
    public const string RateLimited = "rate_limited";
    public const string UnsupportedVersion = "unsupported_version";
    public const string InvalidEnvelope = "invalid_envelope";
    public const string ChannelMismatch = "channel_mismatch";
    public const string SidInUse = "sid_in_use";
    public const string TooManySessions = "too_many_sessions";
    public const string ChannelFull = "channel_full";
    public const string Unpaired = "unpaired";
    public const string TicketFull = "ticket_full";

    public static string For(JoinOutcome outcome) => outcome switch
    {
        JoinOutcome.ChannelFull => ChannelFull,
        JoinOutcome.Unpaired => Unpaired,
        JoinOutcome.TicketFull => TicketFull,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "outcome must be a refusal"),
    };
}
