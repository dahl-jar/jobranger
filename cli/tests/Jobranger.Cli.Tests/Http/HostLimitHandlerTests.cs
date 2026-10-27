using System.Net;
using System.Text;
using Jobranger.Cli.Http;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Http;

public sealed class HostLimitHandlerTests : IDisposable {
    private const string retryAfterHeader = "Retry-After";

    private static readonly Uri url = new("https://jobs.example.test/feed");

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
    private HttpClient? client;

    public void Dispose() => client?.Dispose();

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "60", 60)]
    [InlineData(HttpStatusCode.TooManyRequests, null, 30)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "20", 20)]
    [InlineData(HttpStatusCode.InternalServerError, null, 10)]
    public async Task ShouldHoldNextRequest_WhenHostAnswersWithBackoffStatus(
        HttpStatusCode status, string? retryAfter, int seconds) {
        FakeSourceHandler inner = new(index => index == 0 ? Answer(status, retryAfter) : new HttpResponseMessage());
        await Client(inner).GetAsync(url, TestContext.Current.CancellationToken);

        Task<HttpResponseMessage> next = client!.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal((false, true), await LimiterTiming.StateAroundAsync(time, next, TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public async Task ShouldHoldNextRequest_WhenConnectionFails() {
        FakeSourceHandler inner = new(index => index == 0
            ? throw new HttpRequestException(HttpRequestError.ConnectionError)
            : new HttpResponseMessage());
        await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(inner).GetAsync(url, TestContext.Current.CancellationToken));

        Task<HttpResponseMessage> next = client!.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal((false, true), await LimiterTiming.StateAroundAsync(time, next, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task ShouldNotHoldNextRequest_WhenHostAnswersNotFound() {
        FakeSourceHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        await Client(inner).GetAsync(url, TestContext.Current.CancellationToken);

        Task<HttpResponseMessage> next = client!.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.True(next.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ShouldKeepContentHeaders_WhenBodyIsWatched() {
        FakeSourceHandler inner = new(_ => new HttpResponseMessage {
            Content = new StringContent("ok", Encoding.Latin1),
        });

        using HttpResponseMessage response = await Client(inner).GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(Encoding.Latin1.WebName, response.Content.Headers.ContentType?.CharSet);
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string? retryAfter) {
        HttpResponseMessage response = new(status);
        if (retryAfter is not null) {
            response.Headers.Add(retryAfterHeader, retryAfter);
        }
        return response;
    }

    private HttpClient Client(FakeSourceHandler inner) {
        client = new HttpClient(new HostLimitHandler(new HostLimiter(time), time) { InnerHandler = inner });
        return client;
    }
}
