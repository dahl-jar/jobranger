using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Features.Delivery;

public sealed class TimedStreamTests {
    private static readonly TimeSpan limit = TimeSpan.FromSeconds(30);
    private static readonly byte[] data = [1];

    private readonly FakeTimeProvider time = AdFixtures.Clock();

    [Fact]
    public async Task ShouldReportStall_WhenWriteHangs() {
        using TimedStream stream = new(new WritingStream(token => Task.Delay(Timeout.Infinite, token)), limit, time);
        Task<Exception?> writing = Record.ExceptionAsync(
            () => stream.WriteAsync(data, TestContext.Current.CancellationToken).AsTask()).AsTask();

        Exception? error = await ApiClock.RunAsync(time, writing);

        Assert.IsType<TimeoutException>(error);
        Assert.True(stream.TimedOut);
    }

    [Fact]
    public async Task ShouldNotReportStall_WhenCallerCancelsAtLimit() {
        using CancellationTokenSource caller = new();
        using TimedStream stream = new(new WritingStream(token => {
            time.Advance(limit);
            caller.Cancel();
            return Task.FromCanceled(token);
        }), limit, time);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stream.WriteAsync(data, caller.Token).AsTask());

        Assert.False(stream.TimedOut);
    }

    private sealed class WritingStream(Func<CancellationToken, Task> write) : MemoryStream {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(write(cancellationToken));
    }
}
