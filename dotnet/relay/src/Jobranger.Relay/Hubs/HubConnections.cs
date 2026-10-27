using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Jobranger.Relay.Hubs;

internal sealed class HubConnections {
    private readonly ConcurrentDictionary<string, HubConnectionContext> open = new(StringComparer.Ordinal);

    public void Add(HubConnectionContext connection) => open[connection.ConnectionId] = connection;

    public void Remove(string connectionId) => open.TryRemove(connectionId, out _);

    public HubConnectionContext? Find(string connectionId) => open.GetValueOrDefault(connectionId);
}
