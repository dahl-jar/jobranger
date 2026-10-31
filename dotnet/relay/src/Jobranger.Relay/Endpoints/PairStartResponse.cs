namespace Jobranger.Relay.Endpoints;

internal sealed record PairStartResponse(string Code, DateTimeOffset ExpiresAt, string Channel, string Handle, DateTimeOffset HandleExpiresAt);
