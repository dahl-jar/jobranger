using System.Threading.Channels;
using Jobranger.Relay.Core.Sweeping;

namespace Jobranger.Relay.Core.Tests.Mocks;

internal sealed class RecordingStore(TimeProvider time) : ISweepable {
    private static readonly TimeSpan wait = TimeSpan.FromSeconds(10);

    private readonly Channel<DateTimeOffset> sweeps = Channel.CreateUnbounded<DateTimeOffset>();

    public void Sweep() => sweeps.Writer.TryWrite(time.GetUtcNow());

    public Task<DateTimeOffset> Swept() => sweeps.Reader
        .ReadAsync(TestContext.Current.CancellationToken)
        .AsTask()
        .WaitAsync(wait, TestContext.Current.CancellationToken);
}
