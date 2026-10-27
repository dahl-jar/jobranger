using System.Net;
using System.Threading.RateLimiting;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Tests;

public sealed class RelayRateLimiterTests
{
    private static readonly ConcurrencyLimiterOptions onePermit = new() { PermitLimit = 1, QueueLimit = 0 };

    [Theory]
    [InlineData("2001:db8:1:1::1")]
    [InlineData("2001:db8:1:2::1")]
    public void ShouldReturnPermitsWhenLeaseIsDisposed(string next)
    {
        using RelayRateLimiter limiter = new(new FakeTimeProvider(), limiters: (_, _) => new ConcurrencyLimiter(onePermit));
        limiter.AttemptAcquireForAddress(Client("2001:db8:1:1::1"), RateLimiting.PairStart).Dispose();

        using RateLimitLease lease = limiter.AttemptAcquireForAddress(Client(next), RateLimiting.PairStart);

        Assert.True(lease.IsAcquired);
    }

    private static DefaultHttpContext Client(string address) =>
        new() { Connection = { RemoteIpAddress = IPAddress.Parse(address) } };
}
