namespace Jobranger.Relay.Pairing;

internal sealed record NonceResponse(string Nonce, DateTimeOffset ExpiresAt);
