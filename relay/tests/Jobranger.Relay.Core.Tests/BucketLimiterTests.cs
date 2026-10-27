using System.Threading.RateLimiting;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests;

public sealed class BucketLimiterTests : IDisposable
{
    private const int burst = 5;
    private const int refill = 1;

    private static readonly TimeSpan period = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan partPeriod = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider time = new();
    private readonly BucketLimiter limiter;

    public BucketLimiterTests()
    {
        limiter = new BucketLimiter(burst, refill, period, time);
    }

    [Fact]
    public void ShouldRefuseRequestBeyondBurst()
    {
        Taken(burst);

        using RateLimitLease lease = limiter.AttemptAcquire();

        Assert.False(lease.IsAcquired);
    }

    [Fact]
    public void ShouldReportTimeToNextTokenAsRetryAfter()
    {
        Taken(burst);
        time.Advance(partPeriod);

        using RateLimitLease lease = limiter.AttemptAcquire();

        Assert.Equal((true, period - partPeriod), (lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan left), left));
    }

    [Fact]
    public void ShouldRefillOneTokenEachPeriod()
    {
        Taken(burst);
        time.Advance(period);
        Taken(refill);

        using RateLimitLease lease = limiter.AttemptAcquire();

        Assert.False(lease.IsAcquired);
    }

    [Fact]
    public void ShouldNotRefillAboveBurst()
    {
        time.Advance(period * 10);
        Taken(burst);

        using RateLimitLease lease = limiter.AttemptAcquire();

        Assert.False(lease.IsAcquired);
    }

    [Fact]
    public void ShouldNotBeIdleWhileBucketIsBelowBurst()
    {
        Taken(1);
        time.Advance(period - TimeSpan.FromSeconds(1));

        TimeSpan? idle = limiter.IdleDuration;

        Assert.Null(idle);
    }

    [Fact]
    public void ShouldBeIdleSinceBucketRefilled()
    {
        Taken(2);
        time.Advance((period * 2) + partPeriod);

        TimeSpan? idle = limiter.IdleDuration;

        Assert.Equal(partPeriod, idle);
    }

    [Fact]
    public void ShouldKeepCountingIdleTimeWhileBucketStaysFull()
    {
        Taken(2);
        time.Advance((period * 3) + partPeriod);
        _ = limiter.IdleDuration;
        time.Advance(period);

        TimeSpan? idle = limiter.IdleDuration;

        Assert.Equal((period * 2) + partPeriod, idle);
    }

    [Fact]
    public void ShouldRefillSeveralTokensEachPeriod()
    {
        using BucketLimiter large = new(10, 5, period, time);
        for (int take = 0; take < 10; take++)
        {
            using RateLimitLease taken = large.AttemptAcquire();
        }

        time.Advance((period * 2) + partPeriod);

        TimeSpan? idle = large.IdleDuration;

        Assert.Equal(partPeriod, idle);
    }

    [Fact]
    public async Task ShouldAdmitAsyncRequestWithinBurst()
    {
        using RateLimitLease lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

        Assert.True(lease.IsAcquired);
    }

    public void Dispose() => limiter.Dispose();

    private void Taken(int count)
    {
        for (int take = 0; take < count; take++)
        {
            using RateLimitLease lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired);
        }
    }
}
