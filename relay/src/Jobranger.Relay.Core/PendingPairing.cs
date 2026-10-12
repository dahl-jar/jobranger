namespace Jobranger.Relay.Core;

public sealed record PendingPairing(PairingCode Code, DateTimeOffset ExpiresAt)
{
    public override string ToString() => $"PendingPairing {{ ExpiresAt = {ExpiresAt:O} }}";
}
