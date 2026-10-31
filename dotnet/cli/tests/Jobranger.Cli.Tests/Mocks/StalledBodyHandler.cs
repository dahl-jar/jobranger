using System.IO.Pipelines;
using System.Net;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class StalledBodyHandler(byte[] firstChunk) : HttpMessageHandler {
    private readonly Pipe pipe = new();

    public TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        await pipe.Writer.WriteAsync(firstChunk, cancellationToken).ConfigureAwait(false);
        WatchedStream body = new(pipe.Reader.AsStream(), Reading);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
    }

    private sealed class WatchedStream(Stream inner, TaskCompletionSource reading) : ForwardingStream(inner) {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            reading.TrySetResult();
            return Inner.ReadAsync(buffer, cancellationToken);
        }
    }
}
