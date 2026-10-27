using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class RecordingTime : TimeProvider {
    private readonly FakeTimeProvider fake = new();
    private readonly Channel<TimeSpan> waits = Channel.CreateUnbounded<TimeSpan>();

    public override long TimestampFrequency => fake.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => fake.LocalTimeZone;

    public override DateTimeOffset GetUtcNow() => fake.GetUtcNow();

    public override long GetTimestamp() => fake.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        ITimer timer = fake.CreateTimer(callback, state, dueTime, period);
        waits.Writer.TryWrite(dueTime);
        return timer;
    }

    public void Advance(TimeSpan delta) => fake.Advance(delta);

    public async Task<TimeSpan> NextWaitAsync() =>
        await waits.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TestWaits.Step);

    public async Task WaitForAsync(TimeSpan due) {
        while (await NextWaitAsync() != due) {
        }
    }
}
