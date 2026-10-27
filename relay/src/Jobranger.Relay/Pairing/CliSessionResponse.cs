namespace Jobranger.Relay.Pairing;

internal sealed record CliSessionResponse(string Channel, string Handle, DateTimeOffset HandleExpiresAt);
