namespace Jobranger.Relay.Endpoints;

internal sealed record CliSessionResponse(string Channel, string Handle, DateTimeOffset HandleExpiresAt);
