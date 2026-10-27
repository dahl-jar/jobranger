using System.Net;

namespace Jobranger.Cli.Tests.Mocks;

public sealed class LoopbackOnlyHandlerTests {
    private const string okResponse = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
    private const string externalRedirect =
        "HTTP/1.1 302 Found\r\nLocation: https://api.example.test/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
    private const string loopbackAddress = "127.0.0.1";

    [Theory]
    [InlineData("https://api.example.test/")]
    [InlineData("http://192.0.2.1/")]
    public async Task ShouldThrow_WhenTestCallsNonLoopbackHost(string address) {
        using HttpClient client = new(new LoopbackOnlyHandler());

        HttpRequestException refused = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync(new Uri(address), TestContext.Current.CancellationToken));

        Assert.IsType<InvalidOperationException>(refused.InnerException);
    }

    [Theory]
    [InlineData(loopbackAddress)]
    [InlineData("localhost")]
    public async Task ShouldForwardRequest_WhenHostIsLoopback(string host) {
        using LoopbackServer server = new(okResponse, TestContext.Current.CancellationToken);
        using HttpClient client = new(new LoopbackOnlyHandler());

        using HttpResponseMessage response =
            await client.GetAsync(server.AddressFor(host), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ShouldNotFollowRedirect_WhenServerRedirectsToExternalHost() {
        using LoopbackServer server = new(externalRedirect, TestContext.Current.CancellationToken);
        using HttpClient client = new(new LoopbackOnlyHandler());

        using HttpResponseMessage response =
            await client.GetAsync(server.AddressFor(loopbackAddress), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }
}
