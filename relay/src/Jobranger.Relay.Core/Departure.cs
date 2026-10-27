namespace Jobranger.Relay.Core;

public sealed record Departure(Presence Presence, IReadOnlyList<string> Sids);
