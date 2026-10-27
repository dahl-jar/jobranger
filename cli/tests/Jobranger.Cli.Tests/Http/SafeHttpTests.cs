using System.Net;
using System.Net.Sockets;
using Jobranger.Cli.Http;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Http;

public sealed class SafeHttpTests {
    private const int testTimeoutMs = 10_000;
    private const string userAgentLine = "User-Agent: Mozilla/5.0 ";
    private const string redirect = "HTTP/1.1 302 Found\r\nLocation: /next\r\nContent-Length: 0\r\n\r\n";
    private const string stalledBody = "HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\n";
    private const string empty = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private static readonly TimeSpan readTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan rest = TimeSpan.FromMilliseconds(1);

    private readonly FakeTimeProvider time = new();

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldRefuseConnect_WhenHostResolvesToPrivateAddress() {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using HttpClient client = SafeHttp.CreateSourceClient(new HostLimiter(time), time);

        HttpRequestException refused = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync(new Uri($"http://localhost:{port}/"), TestContext.Current.CancellationToken));

        Assert.Equal((true, false), (refused.InnerException is RefusedRequestException, listener.Pending()));
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldReturnRedirect_WhenServerRedirects() {
        await using RawHttpServer server = new(redirect);
        using HttpClient client = LoopbackClient();

        using HttpResponseMessage response = await client.GetAsync(server.Url("/"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldSendBrowserUserAgent_WhenRequesting() {
        await using RawHttpServer server = new(empty);
        using HttpClient client = LoopbackClient();

        using HttpResponseMessage response = await client.GetAsync(server.Url("/"), TestContext.Current.CancellationToken);

        Assert.Contains(userAgentLine, await server.Request, StringComparison.Ordinal);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldEndRead_WhenBodyStallsForThirtySeconds() {
        await using RawHttpServer server = new(stalledBody);
        using HttpClient client = LoopbackClient();
        using HttpResponseMessage response = await client.GetAsync(
            server.Url("/"), HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        await using Stream body = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        Task<int> read = body.ReadAsync(new byte[1], TestContext.Current.CancellationToken).AsTask();

        time.Advance(readTimeout - rest);
        bool early = read.IsCompleted;
        time.Advance(rest);

        await Assert.ThrowsAnyAsync<IOException>(() => read);
        Assert.False(early);
    }

    private HttpClient LoopbackClient() => SafeHttp.CreateClient(new HostLimiter(time), time, IPAddress.IsLoopback);
}
