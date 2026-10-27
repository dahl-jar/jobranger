using Jobranger.Relay.Core;

namespace Jobranger.Relay.Pairing;

internal static class WebSession
{
    public static WebSessionResponse Open(ChannelId channel, ChannelHandle handles, TimeProvider time)
    {
        DateTimeOffset handleExpires = Expiry.After(time, RelayLimits.HandleLifetime);
        DateTimeOffset ticketExpires = Expiry.After(time, RelayLimits.TicketLifetime);
        return new WebSessionResponse(
            channel.Value,
            handles.Sign(channel, RelayRole.Web, handleExpires),
            handleExpires,
            handles.SignTicket(channel, ticketExpires),
            ticketExpires);
    }
}
