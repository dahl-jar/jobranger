using System.Net;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Updates;

namespace Jobranger.Cli.Tests.Mocks;

[UnsupportedOSPlatform("windows")]
internal sealed class FakeGitHubHandler(HttpStatusCode status, byte[] body) : HttpMessageHandler {
    private static readonly Uri releasesUrl = new(VersionCheck.ReleasesUrl);

    public FakeGitHubHandler(byte[] body) : this(HttpStatusCode.OK, body) {
    }

    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        RequestCount++;
        HttpStatusCode answer = request.RequestUri != releasesUrl ? HttpStatusCode.NotFound
            : request.Headers.UserAgent.Count == 0 ? HttpStatusCode.Forbidden
            : status;
        byte[] content = answer == status ? body : [];
        return Task.FromResult(new HttpResponseMessage(answer) { Content = new ByteArrayContent(content) });
    }
}
