namespace Jobranger.Relay.Endpoints;

internal sealed record NonceResponse(string Nonce, DateTimeOffset ExpiresAt);
