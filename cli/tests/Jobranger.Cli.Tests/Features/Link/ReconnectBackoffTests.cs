using Jobranger.Cli.Features.Link;

namespace Jobranger.Cli.Tests.Features.Link;

public sealed class ReconnectBackoffTests {
    private const int manyFailures = 5;

    [Theory]
    [InlineData(4, 1.0, 8)]
    [InlineData(12, 1.0, 300)]
    [InlineData(4, 0.5, 4)]
    public void ShouldWaitJitteredDoublingDelay_WhenAttemptsFail(int failures, double sample, int seconds) {
        ReconnectBackoff backoff = new();
        for (int failure = 1; failure < failures; failure++) {
            backoff.Next(TimeSpan.Zero, sample, TimeSpan.Zero);
        }

        TimeSpan delay = backoff.Next(TimeSpan.Zero, sample, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromSeconds(seconds), delay);
    }

    [Fact]
    public void ShouldResetDelayToOneSecond_WhenConnectedForSixtySeconds() {
        ReconnectBackoff backoff = new();
        for (int failure = 0; failure < manyFailures; failure++) {
            backoff.Next(TimeSpan.Zero, 1.0, TimeSpan.Zero);
        }

        TimeSpan delay = backoff.Next(TimeSpan.FromSeconds(60), 1.0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromSeconds(1), delay);
    }
}
