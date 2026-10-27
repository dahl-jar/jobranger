using Jobranger.Cli.Http;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Http;

public sealed class HostLimiterTests {
    private const string host = "jobs.example.test";
    private const string otherHost = "other.example.test";
    private const int testTimeoutMs = 10_000;

    private readonly FakeTimeProvider time = new();
    private readonly HostLimiter limiter;

    public HostLimiterTests() {
        limiter = new HostLimiter(time);
    }

    [Fact]
    public async Task ShouldHoldThirdRequest_WhenDefaultBurstIsSpent() {
        await AcquireBurstAsync(host);

        Task third = limiter.AcquireAsync(host, TestContext.Current.CancellationToken);

        Assert.Equal((false, true), await LimiterTiming.StateAroundAsync(time, third, TimeSpan.FromSeconds(1)));
    }

    [Theory(Timeout = testTimeoutMs)]
    [InlineData("www.finn.no", 1, 2000)]
    [InlineData("www.linkedin.com", 1, 3000)]
    [InlineData("pam-stilling-feed.nav.no", 4, 500)]
    public async Task ShouldSpaceRequests_WhenHostHasOwnRule(string ruleHost, int burst, int dueMs) {
        for (int request = 0; request < burst; request++) {
            await limiter.AcquireAsync(ruleHost, TestContext.Current.CancellationToken);
        }

        Task next = limiter.AcquireAsync(ruleHost, TestContext.Current.CancellationToken);

        Assert.Equal((false, true), await LimiterTiming.StateAroundAsync(time, next, TimeSpan.FromMilliseconds(dueMs)));
    }

    [Fact]
    public async Task ShouldLetRequestThrough_WhenIntervalHasPassed() {
        await AcquireBurstAsync(host);
        time.Advance(TimeSpan.FromSeconds(1));

        Task next = limiter.AcquireAsync(host, TestContext.Current.CancellationToken);

        Assert.True(next.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ShouldNotHoldOtherHost_WhenOneHostIsBusy() {
        await AcquireBurstAsync(host);

        Task other = limiter.AcquireAsync(otherHost, TestContext.Current.CancellationToken);

        Assert.True(other.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ShouldWaitRetryAfter_WhenHostIsPenalized() {
        limiter.Penalize(host, TimeSpan.FromSeconds(60));

        Task next = limiter.AcquireAsync(host, TestContext.Current.CancellationToken);

        Assert.Equal((false, true), await LimiterTiming.StateAroundAsync(time, next, TimeSpan.FromSeconds(60)));
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldEndWait_WhenCancelled() {
        limiter.Penalize(host, TimeSpan.FromSeconds(300));
        using CancellationTokenSource cancel =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task next = limiter.AcquireAsync(host, cancel.Token);

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
    }

    private async Task AcquireBurstAsync(string target) {
        await limiter.AcquireAsync(target, TestContext.Current.CancellationToken);
        await limiter.AcquireAsync(target, TestContext.Current.CancellationToken);
    }
}
