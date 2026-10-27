using System.Net;
using System.Text;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeSiteHandler(Func<Uri, HttpResponseMessage> answer) : HttpMessageHandler {
    private readonly List<(Uri Url, string? Authorization)> seen = [];

    public IReadOnlyList<Uri> Urls => [.. seen.Select(request => request.Url)];

    public IReadOnlyList<string?> Authorizations => [.. seen.Select(request => request.Authorization)];

    public Action<Uri>? OnRequest { get; set; }

    public static HttpResponseMessage Body(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) };

    public static HttpResponseMessage Body(byte[] body) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        seen.Add((request.RequestUri!, request.Headers.Authorization?.ToString()));
        OnRequest?.Invoke(request.RequestUri!);
        return Task.FromResult(answer(request.RequestUri!));
    }
}
