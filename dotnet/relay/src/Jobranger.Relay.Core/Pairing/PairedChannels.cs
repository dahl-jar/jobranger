using Jobranger.Messaging.Channels;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Core.Sweeping;

namespace Jobranger.Relay.Core.Pairing;

public sealed class PairedChannels(TimeProvider time, int capacity = RelayLimits.MaxPairedChannels) : ISweepable {
    private readonly Dictionary<ChannelId, LinkedListNode<PairedChannel>> records = [];
    private readonly LinkedList<PairedChannel> byLastUse = new();
    private readonly Lock gate = new();

    internal int Count {
        get {
            lock (gate) {
                return records.Count;
            }
        }
    }

    public void Record(ChannelId channel) {
        lock (gate) {
            Forget(channel);
            if (records.Count >= capacity && byLastUse.First is { } leastRecent) {
                Forget(leastRecent.Value.Channel);
            }

            Remember(channel);
        }
    }

    public bool Touch(ChannelId channel) {
        lock (gate) {
            if (!records.TryGetValue(channel, out LinkedListNode<PairedChannel>? node)) {
                return false;
            }

            Forget(channel);
            if (time.GetUtcNow() >= node.Value.ExpiresAt) {
                return false;
            }

            Remember(channel);
            return true;
        }
    }

    public void Sweep() {
        lock (gate) {
            DateTimeOffset now = time.GetUtcNow();
            while (byLastUse.First is { Value: var oldest } && oldest.ExpiresAt <= now) {
                Forget(oldest.Channel);
            }
        }
    }

    private void Remember(ChannelId channel) =>
        records.Add(channel, byLastUse.AddLast(new PairedChannel(channel, time.GetUtcNow() + RelayLimits.TicketLifetime)));

    private void Forget(ChannelId channel) {
        if (records.Remove(channel, out LinkedListNode<PairedChannel>? node)) {
            byLastUse.Remove(node);
        }
    }

    private sealed record PairedChannel(ChannelId Channel, DateTimeOffset ExpiresAt);
}
