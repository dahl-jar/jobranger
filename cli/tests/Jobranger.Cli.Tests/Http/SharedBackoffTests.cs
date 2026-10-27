using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Http;

public sealed class SharedBackoffTests {
    private readonly FakeTimeProvider time = ApiClock.Create();

    [Fact]
    public void ShouldNotWait_WhenNothingFailed() {
        SharedBackoff backoff = new(time);

        Task waiting = backoff.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(waiting.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ShouldDoublePauseUpToTwoMinutes_WhenFailuresRepeat() {
        SharedBackoff backoff = new(time);
        List<(bool, bool)> pauses = [];

        foreach (int seconds in new[] { 15, 30, 60, 120, 120 }) {
            backoff.Penalize(null);
            pauses.Add(await PauseAsync(backoff, TimeSpan.FromSeconds(seconds)));
        }

        Assert.All(pauses, pause => Assert.Equal((false, true), pause));
    }

    [Fact]
    public async Task ShouldStartPauseAgain_WhenResetAfterSuccess() {
        SharedBackoff backoff = new(time);
        backoff.Penalize(null);
        await PauseAsync(backoff, TimeSpan.FromSeconds(15));
        backoff.Penalize(null);
        await PauseAsync(backoff, TimeSpan.FromSeconds(30));
        backoff.Reset();
        backoff.Penalize(null);

        (bool early, bool onTime) = await PauseAsync(backoff, TimeSpan.FromSeconds(15));

        Assert.Equal((false, true), (early, onTime));
    }

    [Theory]
    [InlineData(30, 30)]
    [InlineData(0, 15)]
    public async Task ShouldPauseForRetryAfter_WhenProviderGivesPositiveValue(int retryAfter, int pause) {
        SharedBackoff backoff = new(time);
        backoff.Penalize(TimeSpan.FromSeconds(retryAfter));

        (bool early, bool onTime) = await PauseAsync(backoff, TimeSpan.FromSeconds(pause));

        Assert.Equal((false, true), (early, onTime));
    }

    [Fact]
    public async Task ShouldKeepLongerPause_WhenShorterPenaltyFollows() {
        SharedBackoff backoff = new(time);
        backoff.Penalize(TimeSpan.FromSeconds(60));
        backoff.Penalize(TimeSpan.FromSeconds(10));

        (bool early, bool onTime) = await PauseAsync(backoff, TimeSpan.FromSeconds(60));

        Assert.Equal((false, true), (early, onTime));
    }

    private Task<(bool Early, bool OnTime)> PauseAsync(SharedBackoff backoff, TimeSpan due) =>
        LimiterTiming.StateAroundAsync(time, backoff.WaitAsync(TestContext.Current.CancellationToken), due);
}
