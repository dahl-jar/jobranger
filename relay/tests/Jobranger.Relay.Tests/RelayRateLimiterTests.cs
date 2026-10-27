using System.Net;
using System.Threading.RateLimiting;
using Jobranger.Relay.Core;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Tests;

public sealed class RelayRateLimiterTests
{
    private const string clientAddress = "2001:db8:1:1::1";
    private const int idlePartitions = 4;

    private static readonly TimeSpan pastEveryWindow = TimeSpan.FromMinutes(10);
    private static readonly ConcurrencyLimiterOptions onePermit = new() { PermitLimit = 1, QueueLimit = 0 };
    private static readonly ConcurrencyLimiterOptions manyPermits = new() { PermitLimit = 1_000, QueueLimit = 0 };

    [Theory]
    [InlineData(clientAddress)]
    [InlineData("2001:db8:1:2::1")]
    public void ShouldReturnPermitsWhenLeaseIsDisposed(string next)
    {
        using RelayRateLimiter limiter = new(new FakeTimeProvider(), limiters: (_, _) => new ConcurrencyLimiter(onePermit));
        limiter.AttemptAcquireForAddress(Client(clientAddress), RateLimiting.PairStart).Dispose();

        using RateLimitLease lease = limiter.AttemptAcquireForAddress(Client(next), RateLimiting.PairStart);

        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public void ShouldDropIdlePartitionsOfEveryTierOnSweep()
    {
        FakeTimeProvider time = new();
        int disposed = 0;
        using RelayRateLimiter limiter = new(
            time,
            overflowShards: 1,
            limiters: (policy, scale) => new DisposalCountingLimiter(
                RateLimiting.Limiter(policy, scale, time), () => Interlocked.Increment(ref disposed)));
        limiter.AttemptAcquireForAddress(Client(clientAddress), RateLimiting.Nonce).Dispose();
        limiter.AttemptAcquireForChannel(Client(clientAddress), new ChannelId("channel-one"), RelayRole.Cli).Dispose();
        limiter.AttemptAcquireForUnpairedChannel(Client(clientAddress), new ChannelId("channel-two")).Dispose();
        time.Advance(pastEveryWindow);

        limiter.Sweep();

        Assert.Equal(idlePartitions, disposed);
    }

    [Fact]
    public void ShouldReturnClientPermitWhenBlockRefuses()
    {
        using RelayRateLimiter limiter = new(new FakeTimeProvider(), limiters: (_, _) => new ConcurrencyLimiter(onePermit));
        RateLimitLease holder = limiter.AttemptAcquireForAddress(Client("2001:db8:1:1::1"), RateLimiting.PairStart);
        limiter.AttemptAcquireForAddress(Client("2001:db8:1:2::1"), RateLimiting.PairStart).Dispose();
        holder.Dispose();

        using RateLimitLease lease = limiter.AttemptAcquireForAddress(Client("2001:db8:1:2::1"), RateLimiting.PairStart);

        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public void ShouldKeepNetworkOverflowInItsOwnShardWhenChannelStoreIsFull()
    {
        using RelayRateLimiter limiter = new(
            new FakeTimeProvider(),
            maxPartitions: 1,
            limiters: (policy, _) => new ConcurrencyLimiter(policy == RateLimiting.UnpairedChannel ? onePermit : manyPermits));
        using RateLimitLease live = UnpairedChannel(limiter, "100.68.6.1");
        using RateLimitLease overflow = UnpairedChannel(limiter, "100.68.6.2");

        using RateLimitLease sameNetwork = UnpairedChannel(limiter, "100.68.6.3");

        Assert.Equal((true, false), (overflow.IsAcquired, sameNetwork.IsAcquired));
    }

    private static RateLimitLease UnpairedChannel(RelayRateLimiter limiter, string address) =>
        limiter.AttemptAcquireForUnpairedChannel(Client(address), RelayClients.NewChannel());

    private static DefaultHttpContext Client(string address) =>
        new() { Connection = { RemoteIpAddress = IPAddress.Parse(address) } };
}
