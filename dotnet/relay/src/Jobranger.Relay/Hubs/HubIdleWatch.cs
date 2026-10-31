using Jobranger.Relay.Core.Limits;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;

namespace Jobranger.Relay.Hubs;

internal sealed class HubIdleWatch(TimeProvider time) {
    public ConnectionDelegate Wrap(ConnectionDelegate next) => async connection => {
        ActivityReader reader = new(connection.Transport.Input, time);
        connection.Transport = new WatchedPipe(reader, connection.Transport.Output);
        connection.Features.Get<IConnectionHeartbeatFeature>()?.OnHeartbeat(_ => {
            if (time.GetUtcNow() - reader.LastRead >= RelayLimits.HubClientTimeout) {
                connection.Abort();
            }
        }, connection);
        await next(connection);
    };
}
