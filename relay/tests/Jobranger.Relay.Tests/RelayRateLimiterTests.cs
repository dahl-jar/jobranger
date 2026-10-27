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
    private const int idlePartitions = 3;

    private static readonly TimeSpan pastEveryWindow = TimeSpan.FromMinutes(10);
    private static readonly ConcurrencyLimiterOptions onePermit = new() { PermitLimit = 1, QueueLimit = 0 };

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
        limiter.AttemptAcquireForChannel(new ChannelId("channel-one"), RelayRole.Cli).Dispose();
        time.Advance(pastEveryWindow);

        limiter.Sweep();

        Assert.Equal(idlePartitions, disposed);
    }

    private static DefaultHttpContext Client(string address) =>
        new() { Connection = { RemoteIpAddress = IPAddress.Parse(address) } };
}
