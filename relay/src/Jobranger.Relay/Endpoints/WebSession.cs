using Jobranger.Contracts.Channels;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Core.Tokens;

namespace Jobranger.Relay.Endpoints;

internal static class WebSession {
    public static WebSessionResponse Open(ChannelId channel, string grant, ChannelHandle handles, TimeProvider time) {
        DateTimeOffset handleExpires = Expiry.After(time, RelayLimits.HandleLifetime);
        DateTimeOffset ticketExpires = Expiry.After(time, RelayLimits.TicketLifetime);
        return new WebSessionResponse(
            channel.Value,
            handles.SignWeb(channel, handleExpires, grant),
            handleExpires,
            handles.SignTicket(channel, ticketExpires, grant),
            ticketExpires);
    }
}
