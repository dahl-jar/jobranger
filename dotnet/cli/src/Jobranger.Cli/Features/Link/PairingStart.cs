namespace Jobranger.Cli.Features.Link;

internal sealed record PairingStart(string Code, DateTimeOffset ExpiresAt, string Handle);
