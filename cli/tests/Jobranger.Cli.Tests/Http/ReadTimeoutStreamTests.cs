using System.IO.Pipelines;
using Jobranger.Cli.Http;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Http;

public sealed class ReadTimeoutStreamTests {
    private const int testTimeoutMs = 10_000;

    private static readonly TimeSpan readTimeout = TimeSpan.FromSeconds(30);

    private readonly FakeTimeProvider time = new();
    private readonly Pipe pipe = new();

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldThrowIOException_WhenReadStallsPastTimeout() {
        using ReadTimeoutStream stream = new(pipe.Reader.AsStream(), readTimeout, time);
        Task<int> read = stream.ReadAsync(new byte[1], TestContext.Current.CancellationToken).AsTask();

        time.Advance(readTimeout);

        await Assert.ThrowsAsync<IOException>(() => read);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldStayCancelled_WhenCallerCancelsAtDeadline() {
        using ReadTimeoutStream stream = new(pipe.Reader.AsStream(), TimeSpan.Zero, time);
        using CancellationTokenSource cancel =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancel.CancelAsync();

        Task<int> read = stream.ReadAsync(new byte[1], cancel.Token).AsTask();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    }

    [Fact]
    public void ShouldDisposeInnerStream_WhenDisposed() {
        MemoryStream inner = new();
        ReadTimeoutStream stream = new(inner, readTimeout, time);

        stream.Dispose();

        Assert.False(inner.CanRead);
    }
}
