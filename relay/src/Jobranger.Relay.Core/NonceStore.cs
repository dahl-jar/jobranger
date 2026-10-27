using System.Buffers.Text;
using System.Security.Cryptography;

namespace Jobranger.Relay.Core;

public sealed class NonceStore(
    TimeProvider time,
    int maxOutstanding = RelayLimits.MaxOutstandingNonces,
    int maxPerNetwork = RelayLimits.MaxOutstandingNoncesPerNetwork,
    int maxPerChannel = RelayLimits.MaxOutstandingNoncesPerChannel)
{
    private const int nonceBytes = 32;
    private const string networkSharePrefix = "network:";
    private const string channelSharePrefix = "channel:";

    private readonly Dictionary<string, LinkedListNode<Outstanding>> outstanding = new(StringComparer.Ordinal);
    private readonly LinkedList<Outstanding> byExpiry = new();
    private readonly Dictionary<string, int> perShare = new(StringComparer.Ordinal);
    private readonly HashSet<string> expired = new(StringComparer.Ordinal);
    private readonly Queue<RelayNonce> expiredByAge = new();
    private readonly Lock gate = new();

    public RelayNonce? IssueForChannel(ChannelId channel) => Issue($"{channelSharePrefix}{channel.Value}", maxPerChannel);

    public RelayNonce? IssueForNetwork(string network) => Issue($"{networkSharePrefix}{network}", maxPerNetwork);

    public NonceOutcome Consume(string? nonce)
    {
        if (nonce is null)
        {
            return NonceOutcome.Unknown;
        }

        lock (gate)
        {
            DateTimeOffset now = time.GetUtcNow();
            if (outstanding.Remove(nonce, out LinkedListNode<Outstanding>? node))
            {
                byExpiry.Remove(node);
                Release(node.Value.Share);
                return now >= node.Value.Nonce.ExpiresAt ? NonceOutcome.Expired : NonceOutcome.Valid;
            }

            return expired.Contains(nonce) ? NonceOutcome.Expired : NonceOutcome.Unknown;
        }
    }

    private RelayNonce? Issue(string share, int shareLimit)
    {
        lock (gate)
        {
            DateTimeOffset now = time.GetUtcNow();
            Drain(now);
            int shareCount = perShare.GetValueOrDefault(share);
            if (outstanding.Count >= maxOutstanding || shareCount >= shareLimit)
            {
                return null;
            }

            RelayNonce nonce = new(
                Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(nonceBytes)),
                now + RelayLimits.NonceLifetime);
            outstanding.Add(nonce.Value, byExpiry.AddLast(new Outstanding(nonce, share)));
            perShare[share] = shareCount + 1;
            return nonce;
        }
    }

    private void Drain(DateTimeOffset now)
    {
        while (byExpiry.First is { Value: var oldest } && oldest.Nonce.ExpiresAt <= now)
        {
            byExpiry.RemoveFirst();
            outstanding.Remove(oldest.Nonce.Value);
            Release(oldest.Share);
            expired.Add(oldest.Nonce.Value);
            expiredByAge.Enqueue(oldest.Nonce);
        }

        while (expiredByAge.TryPeek(out RelayNonce? stale)
            && (expired.Count > maxOutstanding || stale.ExpiresAt + RelayLimits.NonceTombstoneRetention <= now))
        {
            expiredByAge.Dequeue();
            expired.Remove(stale.Value);
        }
    }

    private void Release(string share)
    {
        if (perShare[share] == 1)
        {
            perShare.Remove(share);
        }
        else
        {
            perShare[share]--;
        }
    }

    private sealed record Outstanding(RelayNonce Nonce, string Share);
}
