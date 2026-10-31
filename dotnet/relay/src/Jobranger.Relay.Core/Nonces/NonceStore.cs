using System.Buffers.Text;
using System.Security.Cryptography;
using Jobranger.Messaging.Channels;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Core.Sweeping;

namespace Jobranger.Relay.Core.Nonces;

public sealed class NonceStore(
    TimeProvider time,
    int maxOutstanding = RelayLimits.MaxOutstandingNonces,
    int maxPerNetwork = RelayLimits.MaxOutstandingNoncesPerNetwork,
    int maxPerChannelClient = RelayLimits.MaxOutstandingNoncesPerChannelClient,
    int maxChannelNoncesPerNetwork = RelayLimits.MaxOutstandingChannelNoncesPerNetwork,
    int maxChannelNoncesPerClient = RelayLimits.MaxOutstandingChannelNoncesPerClient) : ISweepable {
    private const int nonceBytes = 32;
    private const string networkSharePrefix = "network:";
    private const string channelSharePrefix = "channel:";
    private const string clientSharePrefix = "client:";
    private const string channelNetworkSharePrefix = "channelnet:";

    private readonly Dictionary<string, LinkedListNode<Outstanding>> outstanding = new(StringComparer.Ordinal);
    private readonly LinkedList<Outstanding> byExpiry = new();
    private readonly Dictionary<string, int> perShare = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Holders> byNetwork = new(StringComparer.Ordinal);
    private readonly HashSet<string> expired = new(StringComparer.Ordinal);
    private readonly Queue<RelayNonce> expiredByAge = new();
    private readonly Lock gate = new();
    private long issued;

    public RelayNonce? IssueForChannel(ChannelId channel, string client, string network) =>
        Issue(
            [
                new Share($"{channelSharePrefix}{channel.Value}:{client}", maxPerChannelClient),
                new Share($"{clientSharePrefix}{client}", maxChannelNoncesPerClient),
            ],
            new Holding(new Share($"{channelNetworkSharePrefix}{network}", maxChannelNoncesPerNetwork), client));

    public RelayNonce? IssueForNetwork(string network) => Issue([new Share($"{networkSharePrefix}{network}", maxPerNetwork)], null);

    public NonceOutcome Consume(string? nonce) {
        if (nonce is null) {
            return NonceOutcome.Unknown;
        }

        lock (gate) {
            DateTimeOffset now = time.GetUtcNow();
            if (outstanding.TryGetValue(nonce, out LinkedListNode<Outstanding>? node)) {
                Remove(node);
                return now >= node.Value.Nonce.ExpiresAt ? NonceOutcome.Expired : NonceOutcome.Valid;
            }

            return expired.Contains(nonce) ? NonceOutcome.Expired : NonceOutcome.Unknown;
        }
    }

    public void Sweep() {
        lock (gate) {
            Drain(time.GetUtcNow());
        }
    }

    private RelayNonce? Issue(Share[] capped, Holding? holding) {
        lock (gate) {
            DateTimeOffset now = time.GetUtcNow();
            Drain(now);
            if (capped.Any(IsFull) || !MakeRoom(holding)) {
                return null;
            }

            RelayNonce nonce = new(
                Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(nonceBytes)),
                now + RelayLimits.NonceLifetime);
            Outstanding entry = new(nonce, holding is { } held ? [.. capped, held.Share] : capped, holding, issued++);
            outstanding.Add(nonce.Value, byExpiry.AddLast(entry));
            foreach (Share share in entry.Shares) {
                perShare[share.Key] = perShare.GetValueOrDefault(share.Key) + 1;
            }

            if (holding is { } holder) {
                HoldersOf(holder.Share.Key).Add(entry, holder.Client);
            }

            return nonce;
        }
    }

    private bool MakeRoom(Holding? holding) {
        if (holding is { } held && IsFull(held.Share)) {
            Expire(outstanding[byNetwork[held.Share.Key].Evictable.Nonce.Value]);
            return true;
        }

        return outstanding.Count < maxOutstanding;
    }

    private void Drain(DateTimeOffset now) {
        while (byExpiry.First is { } oldest && oldest.Value.Nonce.ExpiresAt <= now) {
            Expire(oldest);
        }

        while (expiredByAge.TryPeek(out RelayNonce? stale)
            && (expired.Count > maxOutstanding || stale.ExpiresAt + RelayLimits.NonceTombstoneRetention <= now)) {
            expiredByAge.Dequeue();
            expired.Remove(stale.Value);
        }
    }

    private void Expire(LinkedListNode<Outstanding> node) {
        Remove(node);
        expired.Add(node.Value.Nonce.Value);
        expiredByAge.Enqueue(node.Value.Nonce);
    }

    private void Remove(LinkedListNode<Outstanding> node) {
        byExpiry.Remove(node);
        outstanding.Remove(node.Value.Nonce.Value);
        Release(node.Value);
    }

    private Holders HoldersOf(string networkKey) {
        if (!byNetwork.TryGetValue(networkKey, out Holders? holders)) {
            holders = new Holders();
            byNetwork.Add(networkKey, holders);
        }

        return holders;
    }

    private bool IsFull(Share share) => perShare.GetValueOrDefault(share.Key) >= share.Limit;

    private void Release(Outstanding entry) {
        if (entry is { Holding: { } holding, Held: { } held }) {
            Holders holders = byNetwork[holding.Share.Key];
            holders.Remove(held, holding.Client);
            if (holders.IsEmpty) {
                byNetwork.Remove(holding.Share.Key);
            }
        }

        foreach (Share share in entry.Shares) {
            if (perShare[share.Key] == 1) {
                perShare.Remove(share.Key);
            } else {
                perShare[share.Key]--;
            }
        }
    }

    private sealed record Outstanding(RelayNonce Nonce, Share[] Shares, Holding? Holding, long Sequence) {
        public LinkedListNode<Outstanding>? Held { get; set; }
    }

    private readonly record struct Share(string Key, int Limit);

    private readonly record struct Holding(Share Share, string Client);

    private sealed class Holders {
        private static readonly IComparer<LinkedList<Outstanding>> largestThenOldest = Comparer<LinkedList<Outstanding>>.Create(LargestThenOldest);

        private readonly Dictionary<string, LinkedList<Outstanding>> byClient = new(StringComparer.Ordinal);
        private readonly SortedSet<LinkedList<Outstanding>> largestFirst = new(largestThenOldest);

        public bool IsEmpty => byClient.Count == 0;

        public Outstanding Evictable => largestFirst.Min is { First: { } oldest }
            ? oldest.Value
            : throw new InvalidOperationException("full network channel share has no outstanding nonce to evict");

        public void Add(Outstanding entry, string client) {
            if (!byClient.TryGetValue(client, out LinkedList<Outstanding>? nonces)) {
                nonces = new LinkedList<Outstanding>();
                byClient.Add(client, nonces);
            }

            largestFirst.Remove(nonces);
            entry.Held = nonces.AddLast(entry);
            largestFirst.Add(nonces);
        }

        public void Remove(LinkedListNode<Outstanding> held, string client) {
            LinkedList<Outstanding> nonces = byClient[client];
            largestFirst.Remove(nonces);
            nonces.Remove(held);
            if (nonces.Count == 0) {
                byClient.Remove(client);
            } else {
                largestFirst.Add(nonces);
            }
        }

        private static int LargestThenOldest(LinkedList<Outstanding> left, LinkedList<Outstanding> right) {
            int bySize = right.Count.CompareTo(left.Count);
            return bySize != 0 ? bySize : Oldest(left).CompareTo(Oldest(right));
        }

        private static long Oldest(LinkedList<Outstanding> nonces) => nonces.First is { } first ? first.Value.Sequence : long.MaxValue;
    }
}
