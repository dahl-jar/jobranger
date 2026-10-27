using Jobranger.Relay.Core.Sweeping;
using Jobranger.Relay.Core.Tests.Mocks;

namespace Jobranger.Relay.Core.Tests.Sweeping;

public sealed class SweeperHeartbeatTests {
    private static readonly DateTimeOffset beat = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan oneSecond = TimeSpan.FromSeconds(1);

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void ShouldCountBeatOnlyBeforeLimitPasses(int secondsBeforeLimit, bool within) {
        SweeperHeartbeat heartbeat = new();
        heartbeat.Beat(beat);

        bool reported = heartbeat.BeatWithin(beat + SweeperTiming.StaleAfter - (oneSecond * secondsBeforeLimit), SweeperTiming.StaleAfter);

        Assert.Equal(within, reported);
    }

    [Fact]
    public void ShouldReportNoBeatBeforeFirstBeat() {
        SweeperHeartbeat heartbeat = new();

        bool reported = heartbeat.BeatWithin(beat, SweeperTiming.DeadAfter);

        Assert.False(reported);
    }
}
