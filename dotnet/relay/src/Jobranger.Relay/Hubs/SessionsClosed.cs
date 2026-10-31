namespace Jobranger.Relay.Hubs;

internal sealed record SessionsClosed(IReadOnlyList<string> Sids);
