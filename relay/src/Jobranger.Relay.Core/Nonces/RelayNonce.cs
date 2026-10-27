namespace Jobranger.Relay.Core.Nonces;

public sealed record RelayNonce(string Value, DateTimeOffset ExpiresAt);
