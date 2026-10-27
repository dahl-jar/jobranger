namespace Jobranger.Relay.Pairing;

internal sealed record PairStartResponse(string Code, DateTimeOffset ExpiresAt, string Channel, string Handle, DateTimeOffset HandleExpiresAt);
