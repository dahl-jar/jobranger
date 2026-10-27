using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests;

public sealed class SweeperServiceTests : IDisposable {
    private static readonly TimeSpan oneSecond = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan wait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan pollInterval = TimeSpan.FromMilliseconds(10);

    private readonly FakeTimeProvider time = new();
    private readonly SweeperHeartbeat heartbeat = new();
    private readonly RecordingStore first;
    private readonly RecordingStore second;
    private readonly SweeperService sweeper;

    public SweeperServiceTests() {
        first = new RecordingStore(time);
        second = new RecordingStore(time);
        sweeper = new SweeperService([first, second], heartbeat, time);
    }

    [Fact]
    public async Task ShouldBeatHeartbeatOnStart() {
        await sweeper.StartAsync(TestContext.Current.CancellationToken);

        bool fresh = heartbeat.BeatWithin(time.GetUtcNow(), SweeperTiming.StaleAfter);

        Assert.True(fresh);
    }

    [Fact]
    public async Task ShouldSweepEveryStoreEachPeriod() {
        await sweeper.StartAsync(TestContext.Current.CancellationToken);
        DateTimeOffset tick = time.GetUtcNow() + SweeperTiming.Period;
        time.Advance(SweeperTiming.Period);

        DateTimeOffset[] swept = await Task.WhenAll(first.Swept(), second.Swept());

        Assert.Equal([tick, tick], swept);
    }

    [Fact]
    public async Task ShouldBeatHeartbeatAfterSweep() {
        await sweeper.StartAsync(TestContext.Current.CancellationToken);
        DateTimeOffset tick = time.GetUtcNow() + SweeperTiming.Period;
        time.Advance(SweeperTiming.Period);
        await second.Swept();

        bool fresh = await Eventually(() => heartbeat.BeatWithin(tick + SweeperTiming.StaleAfter - oneSecond, SweeperTiming.StaleAfter));

        Assert.True(fresh);
    }

    public void Dispose() => sweeper.Dispose();

    private static async Task<bool> Eventually(Func<bool> condition) {
        using CancellationTokenSource deadline = new(wait);
        while (!condition()) {
            if (deadline.IsCancellationRequested) {
                return false;
            }

            await Task.Delay(pollInterval, TestContext.Current.CancellationToken);
        }

        return true;
    }
}
