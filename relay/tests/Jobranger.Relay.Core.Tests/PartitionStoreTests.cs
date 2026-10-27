using System.Threading.RateLimiting;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests;

public sealed class PartitionStoreTests : IDisposable {
    private const int cap = 2;
    private const int permits = 2;
    private const int floodSize = 100;

    private const int shards = 4;
    private const int sampleClients = 64;

    private static readonly TimeSpan window = TimeSpan.FromSeconds(60);
    private static readonly byte[] keyOne = Enumerable.Repeat((byte)1, 32).ToArray();
    private static readonly byte[] keyTwo = Enumerable.Repeat((byte)2, 32).ToArray();

    private readonly FakeTimeProvider time = new();
    private readonly PartitionStore store;

    public PartitionStoreTests() {
        store = new PartitionStore(() => new WindowLimiter(permits, window, time), cap, time, overflowShards: 1, shardKey: keyOne);
    }

    [Fact]
    public void ShouldKeepLivePartitionsAtCapDuringFlood() {
        for (int client = 0; client < floodSize; client++) {
            Acquired($"client-{client}");
        }

        int live = store.LiveCount;

        Assert.Equal(cap, live);
    }

    [Fact]
    public void ShouldKeepExistingClientWindowDuringFlood() {
        Acquired("client-a");
        Acquired("client-b");
        Flood();

        bool granted = Acquired("client-a");

        Assert.True(granted);
    }

    [Fact]
    public void ShouldRefuseOverflowClientsAtPerClientLimit() {
        Acquired("client-a");
        Acquired("client-b");
        Acquired("overflow-one");
        Acquired("overflow-two");

        bool granted = Acquired("overflow-three");

        Assert.False(granted);
    }

    [Fact]
    public void ShouldFreePartitionsOnceTheirWindowsEnd() {
        Acquired("client-a");
        Acquired("client-b");
        time.Advance(window);
        Acquired("client-c");

        int live = store.LiveCount;

        Assert.Equal(1, live);
    }

    [Fact]
    public void ShouldDropIdlePartitionsOnSweep() {
        Acquired("client-a");
        Acquired("client-b");
        time.Advance(window);
        store.Sweep();

        int live = store.LiveCount;

        Assert.Equal(0, live);
    }

    [Fact]
    public void ShouldKeepActivePartitionAcrossSweep() {
        Acquired("client-a");
        Acquired("client-a");
        time.Advance(TimeSpan.FromSeconds(1));
        Acquired("client-b");

        bool granted = Acquired("client-a");

        Assert.False(granted);
    }

    [Fact]
    public void ShouldAdmitClientInOtherShardWhenOneIsExhausted() {
        using PartitionStore sharded = Sharded(keyOne);
        Acquired(sharded, "client-a");
        string exhausted = "flood-0";
        string other = Enumerable.Range(1, sampleClients).Select(client => $"flood-{client}")
            .First(client => sharded.ShardOf(client) != sharded.ShardOf(exhausted));
        Acquired(sharded, exhausted);

        (bool, bool) outcomes = (Acquired(sharded, exhausted), Acquired(sharded, other));

        Assert.Equal((false, true), outcomes);
    }

    [Fact]
    public void ShouldRefuseNewClientOnceEveryShardIsExhausted() {
        using PartitionStore sharded = Sharded(keyOne);
        Acquired(sharded, "client-a");
        for (int client = 0; client < floodSize; client++) {
            Acquired(sharded, $"flood-{client}");
        }

        (bool, int) outcome = (Acquired(sharded, "late-client"), sharded.LiveCount);

        Assert.Equal((false, 1), outcome);
    }

    [Fact]
    public void ShouldConfineNetworkFloodToItsOwnShard() {
        using PartitionStore sharded = Sharded(keyOne);
        Acquired(sharded, "client-a");
        string floodNetwork = "flood-net";
        string otherNetwork = Enumerable.Range(0, sampleClients).Select(network => $"net-{network}")
            .First(network => sharded.ShardOf(network) != sharded.ShardOf(floodNetwork));
        for (int client = 0; client < floodSize; client++) {
            AcquiredIn(sharded, $"flood-{client}", floodNetwork);
        }

        (bool, bool) outcomes = (AcquiredIn(sharded, "late-in-flood", floodNetwork), AcquiredIn(sharded, "late-elsewhere", otherNetwork));

        Assert.Equal((false, true), outcomes);
    }

    [Fact]
    public void ShouldPickSameShardForSameClientUnderSameKey() {
        using PartitionStore first = Sharded(keyOne);
        using PartitionStore second = Sharded(keyOne);

        int differing = Enumerable.Range(0, sampleClients).Count(client => first.ShardOf($"c{client}") != second.ShardOf($"c{client}"));

        Assert.Equal(0, differing);
    }

    [Fact]
    public void ShouldPickShardsByKey() {
        using PartitionStore first = Sharded(keyOne);
        using PartitionStore second = Sharded(keyTwo);

        int differing = Enumerable.Range(0, sampleClients).Count(client => first.ShardOf($"c{client}") != second.ShardOf($"c{client}"));

        Assert.NotEqual(0, differing);
    }

    [Fact]
    public void ShouldSpreadClientsOverEveryShard() {
        using PartitionStore sharded = Sharded(keyOne);

        int used = Enumerable.Range(0, sampleClients).Select(client => sharded.ShardOf($"c{client}")).Distinct().Count();

        Assert.Equal(shards, used);
    }

    public void Dispose() => store.Dispose();

    private static bool Acquired(PartitionStore target, string key) {
        using RateLimitLease lease = target.AttemptAcquire(key);
        return lease.IsAcquired;
    }

    private static bool AcquiredIn(PartitionStore target, string key, string network) {
        using RateLimitLease lease = target.AttemptAcquire(key, network);
        return lease.IsAcquired;
    }

    private PartitionStore Sharded(byte[] key) =>
        new(() => new WindowLimiter(1, window, time), 1, time, overflowShards: shards, shardKey: key);

    private void Flood() {
        for (int client = 0; client < floodSize; client++) {
            Acquired($"flood-{client}");
        }
    }

    private bool Acquired(string key) {
        using RateLimitLease lease = store.AttemptAcquire(key);
        return lease.IsAcquired;
    }
}
