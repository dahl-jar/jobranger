using System.Net;
using System.Threading.RateLimiting;
using Jobranger.Relay.Core;

namespace Jobranger.Relay.Security;

public sealed class RelayRateLimiter : PartitionedRateLimiter<HttpContext>, ISweepable {
    private const int clientScale = 1;
    private const int sharedTiersPerPolicy = 2;
    private const int unpairedChannelStores = 1;

    private static readonly int storesPerBudget = (sharedTiersPerPolicy * RateLimiting.Policies.Length) + unpairedChannelStores;

    private readonly Dictionary<string, PartitionStore> clients;
    private readonly Dictionary<string, PartitionStore> blocks;
    private readonly PartitionStore channels;
    private readonly PartitionStore unpairedChannels;

    public RelayRateLimiter(
        TimeProvider time,
        int? maxPartitions = null,
        int overflowShards = RelayLimits.OverflowShards,
        Func<string, int, RateLimiter>? limiters = null) {
        Func<string, int, RateLimiter> limiterFor = limiters ?? ((policy, scale) => RateLimiting.Limiter(policy, scale, time));
        int partitions = maxPartitions ?? MaxPartitionsPerStore;
        clients = RateLimiting.Policies.ToDictionary(
            policy => policy,
            policy => new PartitionStore(() => limiterFor(policy, clientScale), partitions, time, overflowShards));
        blocks = RateLimiting.Policies.ToDictionary(
            policy => policy,
            policy => new PartitionStore(() => limiterFor(policy, RelayLimits.BlockLimitScale), partitions, time, overflowShards));
        channels = new PartitionStore(
            () => limiterFor(RateLimiting.SessionChannel, clientScale),
            RelayLimits.MaxChannelPartitions,
            time,
            overflowShards);
        unpairedChannels = new PartitionStore(() => limiterFor(RateLimiting.UnpairedChannel, clientScale), partitions, time, overflowShards);
    }

    public static int MaxPartitionsPerStore => RelayLimits.RateLimitStateBudgetBytes / (storesPerBudget * RelayLimits.PartitionBytes);

    public override RateLimiterStatistics? GetStatistics(HttpContext resource) => null;

    public void Sweep() {
        foreach (PartitionStore store in Stores()) {
            store.Sweep();
        }
    }

    public RateLimitLease AttemptAcquireForChannel(HttpContext resource, ChannelId channel, RelayRole role) =>
        channels.AttemptAcquire(
            $"{RelayRoles.Wire(role)}:{channel.Value}", ClientPartition.Network(resource.Connection.RemoteIpAddress));

    public RateLimitLease AttemptAcquireForUnpairedChannel(HttpContext resource, ChannelId channel) =>
        AttemptAcquireTiered(resource.Connection.RemoteIpAddress, RateLimiting.Nonce, unpairedChannels, channel.Value);

    public RateLimitLease AttemptAcquireForAddress(HttpContext resource, string policy) {
        IPAddress? address = resource.Connection.RemoteIpAddress;
        return AttemptAcquireTiered(address, policy, blocks[policy], BlockOf(address, policy));
    }

    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount) =>
        resource.GetEndpoint()?.Metadata.GetMetadata<RelayRateLimit>()?.Policy is { } policy
            ? AttemptAcquireForAddress(resource, policy)
            : GrantedLease.Instance;

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        HttpContext resource, int permitCount, CancellationToken cancellationToken) =>
        ValueTask.FromResult(AttemptAcquireCore(resource, permitCount));

    protected override void Dispose(bool disposing) {
        if (disposing) {
            foreach (PartitionStore store in Stores()) {
                store.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private IEnumerable<PartitionStore> Stores() => clients.Values.Concat(blocks.Values).Append(channels).Append(unpairedChannels);

    private static string? BlockOf(IPAddress? address, string policy) =>
        policy == RateLimiting.PairStart ? ClientPartition.Network(address) : ClientPartition.Ipv6Block(address);

    private RateLimitLease AttemptAcquireTiered(IPAddress? address, string policy, PartitionStore shared, string? sharedKey) {
        string network = ClientPartition.Network(address);
        RateLimitLease clientLease = clients[policy].AttemptAcquire(ClientPartition.For(address), network);
        if (!clientLease.IsAcquired || sharedKey is null) {
            return clientLease;
        }

        RateLimitLease sharedLease = shared.AttemptAcquire(sharedKey, network);
        if (sharedLease.IsAcquired) {
            return new TieredLease(sharedLease, clientLease);
        }

        clientLease.Dispose();
        return sharedLease;
    }

    private sealed class TieredLease(RateLimitLease shared, RateLimitLease client) : RateLimitLease {
        public override bool IsAcquired => client.IsAcquired;

        public override IEnumerable<string> MetadataNames => client.MetadataNames;

        public override bool TryGetMetadata(string metadataName, out object? metadata) =>
            client.TryGetMetadata(metadataName, out metadata);

        protected override void Dispose(bool disposing) {
            if (disposing) {
                shared.Dispose();
                client.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
