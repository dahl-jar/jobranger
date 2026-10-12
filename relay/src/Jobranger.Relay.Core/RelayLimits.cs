namespace Jobranger.Relay.Core;

public static class RelayLimits
{
    public const int MaxWrongGuesses = 5;
    public const int MaxPendingPairings = 2_000;
    public const int MaxPairingEntries = 20_000;
    public const int MaxCodeDrawAttempts = 32;
    public const int MinHandleKeyBytes = 32;

    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan TombstoneRetention = TimeSpan.FromMinutes(10);
}
