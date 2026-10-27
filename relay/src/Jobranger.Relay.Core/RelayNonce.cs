namespace Jobranger.Relay.Core;

public sealed record RelayNonce(string Value, DateTimeOffset ExpiresAt);
