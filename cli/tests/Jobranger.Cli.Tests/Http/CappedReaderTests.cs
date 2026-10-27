using System.IO.Pipelines;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Tests.Http;

public sealed class CappedReaderTests {
    private const long maxBytes = 1000;
    private const int testTimeoutMs = 10_000;

    [Fact]
    public async Task ShouldReturnWholeBody_WhenBodyIsAtCap() {
        await using LargeBodyStream body = new(maxBytes);

        byte[] read = await CappedReader.ReadAsync(body, maxBytes, TestContext.Current.CancellationToken);

        Assert.Equal(maxBytes, read.LongLength);
    }

    [Fact]
    public async Task ShouldThrowTooLarge_WhenBodyPassesCapByOneByte() {
        await using LargeBodyStream body = new(maxBytes + 1);

        await Assert.ThrowsAsync<BodyTooLargeException>(
            () => CappedReader.ReadAsync(body, maxBytes, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldEndRead_WhenStalledBodyIsCancelled() {
        Pipe pipe = new();
        await pipe.Writer.WriteAsync("[{"u8.ToArray(), TestContext.Current.CancellationToken);
        await using Stream body = pipe.Reader.AsStream();
        using CancellationTokenSource cancel =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<byte[]> read = CappedReader.ReadAsync(body, maxBytes, cancel.Token);

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    }
}
