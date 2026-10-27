namespace Jobranger.Relay.Core;

public static class RelayLimits
{
    public const int MaxWrongGuesses = 5;
    public const int MaxPendingPairings = 2_000;
    public const int MaxPendingPairingsPerNetwork = 64;
    public const int MaxPairingEntries = 20_000;
    public const int MaxCodeDrawAttempts = 32;
    public const int MinHandleKeyBytes = 32;
    public const int MaxOutstandingNonces = 100_000;
    public const int MaxOutstandingNoncesPerNetwork = 64;
    public const int MaxOutstandingNoncesPerChannel = 4;
    public const int NonceRequestsPerWindow = 30;
    public const int PairStartsPerWindow = 10;
    public const int FailedSessionsPerWindow = 120;
    public const int SessionsPerChannelWindow = 30;
    public const int ClaimBurst = 5;
    public const int ClaimRefillTokens = 1;
    public const int BlockLimitScale = 5;
    public const int RateLimitStateBudgetBytes = 80 * 1024 * 1024;
    public const int RateLimitStores = 8;
    public const int PartitionBytes = 512;
    public const int MaxPartitionsPerPolicy = RateLimitStateBudgetBytes / (RateLimitStores * PartitionBytes);
    public const int OverflowShards = 256;
    public const int MaxPairedChannels = 20_000;
    public const int SessionRoles = 2;
    public const int MaxChannelPartitions = MaxPairedChannels * SessionRoles;

    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan TombstoneRetention = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan NonceLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan NonceTombstoneRetention = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan HandleLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromDays(90);
    public static readonly TimeSpan NonceRequestWindow = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan PairStartWindow = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan SessionRequestWindow = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ClaimRefillPeriod = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan PartitionSweepInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan RefusalLogInterval = TimeSpan.FromSeconds(1);
}
