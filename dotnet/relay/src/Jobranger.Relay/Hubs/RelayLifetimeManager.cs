using Microsoft.AspNetCore.SignalR;

namespace Jobranger.Relay.Hubs;

internal sealed class RelayLifetimeManager(HubConnections connections, ILogger<DefaultHubLifetimeManager<RelayHub>> logger)
    : DefaultHubLifetimeManager<RelayHub>(logger) {
    public override Task OnConnectedAsync(HubConnectionContext connection) {
        connections.Add(connection);
        return base.OnConnectedAsync(connection);
    }

    public override Task OnDisconnectedAsync(HubConnectionContext connection) {
        connections.Remove(connection.ConnectionId);
        return base.OnDisconnectedAsync(connection);
    }
}
