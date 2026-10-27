using System.IO.Pipelines;
using System.Net;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class StalledBodyHandler(byte[] firstChunk) : HttpMessageHandler {
    private readonly Pipe pipe = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        await pipe.Writer.WriteAsync(firstChunk, cancellationToken).ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(pipe.Reader.AsStream()) };
    }
}
