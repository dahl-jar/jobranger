using Jobranger.Cli.Http;

namespace Jobranger.Cli.Tests.Http;

public sealed class PenalizingStreamTests {
    private static readonly byte[] data = [1];

    [Fact]
    public async Task ShouldRefuseWrite_WhenWrittenAsync() {
        MemoryStream inner = new();
        using PenalizingStream stream = new(inner, () => { });

        await Assert.ThrowsAsync<NotSupportedException>(
            () => stream.WriteAsync(data, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(0, inner.Length);
    }

    [Fact]
    public async Task ShouldLeaveInnerStreamUnflushed_WhenFlushedAsync() {
        FlushCountingStream inner = new();
        using PenalizingStream stream = new(inner, () => { });

        await stream.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, inner.Flushes);
    }

    private sealed class FlushCountingStream : MemoryStream {
        public int Flushes { get; private set; }

        public override Task FlushAsync(CancellationToken cancellationToken) {
            Flushes++;
            return Task.CompletedTask;
        }
    }
}
