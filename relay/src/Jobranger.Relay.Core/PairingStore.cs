using System.Security.Cryptography;
using System.Text;

namespace Jobranger.Relay.Core;

public sealed class PairingStore(
    TimeProvider time,
    int maxPending = RelayLimits.MaxPendingPairings,
    int maxPendingPerNetwork = RelayLimits.MaxPendingPairingsPerNetwork,
    int maxEntries = RelayLimits.MaxPairingEntries,
    Func<PairingCode>? drawCode = null) : ISweepable
{
    private static readonly ClaimResult wrongCode = new(ClaimOutcome.WrongCode, null);
    private static readonly ClaimResult expired = new(ClaimOutcome.Expired, null);
    private static readonly Comparer<Entry> expiryOrder = Comparer<Entry>.Create(CompareExpiry);

    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<ChannelId, Entry> pendingByChannel = [];
    private readonly Dictionary<string, int> pendingByNetwork = new(StringComparer.Ordinal);
    private readonly SortedSet<Entry> lapsing = new(expiryOrder);
    private readonly SortedSet<Entry> retiring = new(expiryOrder);
    private readonly Lock gate = new();
    private readonly Func<PairingCode> nextCode = drawCode ?? PairingCode.Generate;

    public PendingPairing? Start(ChannelId channel, string network)
    {
        lock (gate)
        {
            DateTimeOffset now = time.GetUtcNow();
            Drain(now);
            pendingByChannel.TryGetValue(channel, out Entry? previous);
            if (!TryMakeRoom(previous, network) || !TryDrawFreeCode(out PairingCode code))
            {
                return null;
            }

            Entry entry = new(code, channel, network, now + RelayLimits.CodeLifetime);
            if (previous is not null)
            {
                Replace(previous, entry);
            }

            entries.Add(code.Selector, entry);
            lapsing.Add(entry);
            pendingByChannel.Add(channel, entry);
            pendingByNetwork[network] = pendingByNetwork.GetValueOrDefault(network) + 1;
            return new PendingPairing(code, entry.ExpiresAt);
        }
    }

    public ClaimResult Claim(PairingCode code)
    {
        if (code.Selector is null || code.Verifier is null)
        {
            return wrongCode;
        }

        lock (gate)
        {
            if (!entries.TryGetValue(code.Selector, out Entry? entry))
            {
                return wrongCode;
            }

            if (!entry.Open || time.GetUtcNow() >= entry.ExpiresAt)
            {
                return expired;
            }

            if (!CryptographicOperations.FixedTimeEquals(entry.Verifier, Encoding.ASCII.GetBytes(code.Verifier)))
            {
                entry.Failures++;
                if (entry.Failures >= RelayLimits.MaxWrongGuesses)
                {
                    Close(entry);
                }

                return wrongCode;
            }

            Close(entry);
            return new ClaimResult(ClaimOutcome.Claimed, entry.Channel);
        }
    }

    public void Sweep()
    {
        lock (gate)
        {
            Drain(time.GetUtcNow());
        }
    }

    private static int CompareExpiry(Entry left, Entry right)
    {
        int byExpiry = left.ExpiresAt.CompareTo(right.ExpiresAt);
        return byExpiry != 0 ? byExpiry : string.CompareOrdinal(left.Selector, right.Selector);
    }

    private bool TryMakeRoom(Entry? previous, string network)
    {
        int othersPending = pendingByChannel.Count - (previous is null ? 0 : 1);
        int networkPending = pendingByNetwork.GetValueOrDefault(network) - (previous?.Network == network ? 1 : 0);
        return othersPending < maxPending
            && networkPending < maxPendingPerNetwork
            && (previous is not null || entries.Count < maxEntries || TryEvictOldestTombstone());
    }

    private bool TryDrawFreeCode(out PairingCode code)
    {
        for (int attempt = 0; attempt < RelayLimits.MaxCodeDrawAttempts; attempt++)
        {
            code = nextCode();
            if (!entries.ContainsKey(code.Selector))
            {
                return true;
            }
        }

        code = default;
        return false;
    }

    private void Drain(DateTimeOffset now)
    {
        while (lapsing.Min is { } entry && entry.ExpiresAt <= now)
        {
            Close(entry);
        }

        while (retiring.Min is { } entry && entry.ExpiresAt + RelayLimits.TombstoneRetention <= now)
        {
            Forget(entry);
        }
    }

    private void Close(Entry entry)
    {
        entry.Open = false;
        lapsing.Remove(entry);
        pendingByChannel.Remove(entry.Channel);
        Release(entry.Network);
        retiring.Add(entry);
    }

    private void Release(string network)
    {
        if (pendingByNetwork[network] == 1)
        {
            pendingByNetwork.Remove(network);
        }
        else
        {
            pendingByNetwork[network]--;
        }
    }

    private bool TryEvictOldestTombstone()
    {
        if (retiring.Min is not { } oldest)
        {
            return false;
        }

        Forget(oldest);
        return true;
    }

    private void Replace(Entry previous, Entry next)
    {
        Close(previous);
        Forget(previous.Replaced);
        previous.Replaced = null;
        next.Replaced = previous;
    }

    private void Forget(Entry? entry)
    {
        if (entry is not null && entries.TryGetValue(entry.Selector, out Entry? held) && ReferenceEquals(held, entry))
        {
            entries.Remove(entry.Selector);
            retiring.Remove(entry);
        }
    }

    private sealed class Entry(PairingCode code, ChannelId channel, string network, DateTimeOffset expiresAt)
    {
        public string Selector { get; } = code.Selector;

        public byte[] Verifier { get; } = Encoding.ASCII.GetBytes(code.Verifier);

        public ChannelId Channel { get; } = channel;

        public string Network { get; } = network;

        public DateTimeOffset ExpiresAt { get; } = expiresAt;

        public bool Open { get; set; } = true;

        public int Failures { get; set; }

        public Entry? Replaced { get; set; }
    }
}
