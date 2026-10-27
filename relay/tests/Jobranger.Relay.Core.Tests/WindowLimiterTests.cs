using System.Threading.RateLimiting;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests;

public sealed class WindowLimiterTests : IDisposable {
    private const int permits = 2;

    private static readonly TimeSpan window = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan partWindow = TimeSpan.FromSeconds(25);

    private readonly FakeTimeProvider time = new();
    private readonly WindowLimiter limiter;

    public WindowLimiterTests() {
        limiter = new WindowLimiter(permits, window, time);
    }

    [Fact]
    public void ShouldRefuseRequestBeyondPermitsInWindow() {
        Taken(permits);

        using RateLimitLease lease = limiter.AttemptAcquire();

        Assert.False(lease.IsAcquired);
    }

    [Fact]
    public void ShouldReportTimeLeftInWindowAsRetryAfter() {
        Taken(permits);
        time.Advance(partWindow);

        using RateLimitLease lease = limiter.AttemptAcquire();

        Assert.Equal((true, window - partWindow), (lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan left), left));
    }

    [Fact]
    public void ShouldAdmitAgainOnceWindowEnds() {
        Taken(permits);
        time.Advance(window);

        using RateLimitLease lease = limiter.AttemptAcquire();

        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public void ShouldNotBeIdleWhileWindowHoldsUsedPermits() {
        Taken(1);
        time.Advance(window - TimeSpan.FromSeconds(1));

        TimeSpan? idle = limiter.IdleDuration;

        Assert.Null(idle);
    }

    [Fact]
    public void ShouldBeIdleSinceWindowEnded() {
        Taken(1);
        time.Advance(window + partWindow);

        TimeSpan? idle = limiter.IdleDuration;

        Assert.Equal(partWindow, idle);
    }

    [Fact]
    public void ShouldBeIdleFromWindowEnd() {
        Taken(1);
        time.Advance(window);

        TimeSpan? idle = limiter.IdleDuration;

        Assert.Equal(TimeSpan.Zero, idle);
    }

    [Fact]
    public void ShouldOfferOnlyRetryAfterOnRefusal() {
        Taken(permits);

        using RateLimitLease lease = limiter.AttemptAcquire();

        Assert.Equal(
            (MetadataName.RetryAfter.Name, false),
            (string.Join(',', lease.MetadataNames), lease.TryGetMetadata(MetadataName.ReasonPhrase.Name, out _)));
    }

    [Fact]
    public void ShouldOfferNoMetadataOnGrant() {
        using RateLimitLease lease = limiter.AttemptAcquire();

        Assert.Equal(
            (0, false),
            (lease.MetadataNames.Count(), lease.TryGetMetadata(MetadataName.RetryAfter.Name, out _)));
    }

    [Fact]
    public async Task ShouldAdmitAsyncRequestWithinPermits() {
        using RateLimitLease lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

        Assert.True(lease.IsAcquired);
    }

    public void Dispose() => limiter.Dispose();

    private void Taken(int count) {
        for (int take = 0; take < count; take++) {
            using RateLimitLease lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired);
        }
    }
}
