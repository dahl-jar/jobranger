namespace Jobranger.Relay.Core.Sweeping;

public sealed class SweeperHeartbeat {
    private long lastBeatTicks;

    public void Beat(DateTimeOffset at) => Volatile.Write(ref lastBeatTicks, at.UtcTicks);

    public bool BeatWithin(DateTimeOffset now, TimeSpan limit) =>
        now - new DateTimeOffset(Volatile.Read(ref lastBeatTicks), TimeSpan.Zero) < limit;
}
