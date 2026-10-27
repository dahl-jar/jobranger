using System.Net;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeHttpHandler(IReadOnlyDictionary<Uri, byte[]> bodies) : HttpMessageHandler {
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        RequestCount++;
        if (request.RequestUri is Uri target && bodies.TryGetValue(target, out byte[]? body)) {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
