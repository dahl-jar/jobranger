using System.IO.Pipelines;
using System.Net;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class TrickleBodyHandler(byte[] content, TimeSpan gap, TimeProvider time) : HttpMessageHandler {
    private const int oneUnreadByte = 1;

    private readonly Pipe pipe = new(
        new PipeOptions(pauseWriterThreshold: oneUnreadByte, resumeWriterThreshold: oneUnreadByte));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        _ = WriteAsync(cancellationToken);
        return Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(pipe.Reader.AsStream()) });
    }

    private async Task WriteAsync(CancellationToken cancellationToken) {
        foreach (byte value in content) {
            await pipe.Writer.WriteAsync(new[] { value }, cancellationToken).ConfigureAwait(false);
            await Task.Delay(gap, time, cancellationToken).ConfigureAwait(false);
        }
        await pipe.Writer.CompleteAsync().ConfigureAwait(false);
    }
}
