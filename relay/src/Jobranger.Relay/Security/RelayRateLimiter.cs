using System.Net;
using System.Threading.RateLimiting;
using Jobranger.Relay.Core;

namespace Jobranger.Relay.Security;

public sealed class RelayRateLimiter : PartitionedRateLimiter<HttpContext>
{
    private const int clientScale = 1;

    private readonly Dictionary<string, PartitionStore> clients;
    private readonly Dictionary<string, PartitionStore> blocks;
    private readonly PartitionStore channels;

    public RelayRateLimiter(
        TimeProvider time,
        int maxPartitions = RelayLimits.MaxPartitionsPerPolicy,
        int overflowShards = RelayLimits.OverflowShards,
        Func<string, int, RateLimiter>? limiters = null)
    {
        Func<string, int, RateLimiter> limiterFor = limiters ?? ((policy, scale) => RateLimiting.Limiter(policy, scale, time));
        clients = RateLimiting.Policies.ToDictionary(
            policy => policy,
            policy => new PartitionStore(() => limiterFor(policy, clientScale), maxPartitions, time, overflowShards));
        blocks = RateLimiting.Policies.ToDictionary(
            policy => policy,
            policy => new PartitionStore(() => limiterFor(policy, RelayLimits.BlockLimitScale), maxPartitions, time, overflowShards));
        channels = new PartitionStore(
            () => limiterFor(RateLimiting.SessionChannel, clientScale),
            RelayLimits.MaxChannelPartitions,
            time,
            overflowShards);
    }

    public override RateLimiterStatistics? GetStatistics(HttpContext resource) => null;

    public RateLimitLease AttemptAcquireForChannel(ChannelId channel, RelayRole role) =>
        channels.AttemptAcquire($"{RelayRoles.Wire(role)}:{channel.Value}");

    public RateLimitLease AttemptAcquireForAddress(HttpContext resource, string policy)
    {
        PartitionStore perClient = clients[policy];
        IPAddress? address = resource.Connection.RemoteIpAddress;
        RateLimitLease? blockLease = ClientPartition.Ipv6Block(address) is { } block ? blocks[policy].AttemptAcquire(block) : null;
        if (blockLease is { IsAcquired: false })
        {
            return blockLease;
        }

        RateLimitLease clientLease = perClient.AttemptAcquire(ClientPartition.For(address), ClientPartition.Network(address));
        return blockLease is null ? clientLease : new TieredLease(blockLease, clientLease);
    }

    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount) =>
        resource.GetEndpoint()?.Metadata.GetMetadata<RelayRateLimit>()?.Policy is { } policy
            ? AttemptAcquireForAddress(resource, policy)
            : GrantedLease.Instance;

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        HttpContext resource, int permitCount, CancellationToken cancellationToken) =>
        ValueTask.FromResult(AttemptAcquireCore(resource, permitCount));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (PartitionStore store in clients.Values.Concat(blocks.Values))
            {
                store.Dispose();
            }

            channels.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class TieredLease(RateLimitLease block, RateLimitLease client) : RateLimitLease
    {
        public override bool IsAcquired => client.IsAcquired;

        public override IEnumerable<string> MetadataNames => client.MetadataNames;

        public override bool TryGetMetadata(string metadataName, out object? metadata) =>
            client.TryGetMetadata(metadataName, out metadata);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                block.Dispose();
                client.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
