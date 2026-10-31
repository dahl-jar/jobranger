namespace Jobranger.Relay.Core.Channels;

public sealed record Departure(Presence Presence, IReadOnlyList<string> Sids);
