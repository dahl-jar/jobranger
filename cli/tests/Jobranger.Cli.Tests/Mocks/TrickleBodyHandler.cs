using System.IO.Pipelines;
using System.Net;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class TrickleBodyHandler(byte[] content, TimeSpan gap) : HttpMessageHandler {
    private readonly Pipe pipe = new();

    public Task Writing { get; private set; } = Task.CompletedTask;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        Writing = WriteAsync(cancellationToken);
        return Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(pipe.Reader.AsStream()) });
    }

    private async Task WriteAsync(CancellationToken cancellationToken) {
        foreach (byte value in content) {
            await Task.Delay(gap, cancellationToken).ConfigureAwait(false);
            await pipe.Writer.WriteAsync(new[] { value }, cancellationToken).ConfigureAwait(false);
        }
        await pipe.Writer.CompleteAsync().ConfigureAwait(false);
    }
}
