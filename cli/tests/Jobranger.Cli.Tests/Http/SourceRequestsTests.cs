using System.IO.Pipelines;
using System.Net;
using Jobranger.Cli.Http;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Http;

public sealed class SourceRequestsTests {
    private const long tenMebibytes = 10 * 1024 * 1024;
    private const long hostileBodyBytes = 400L * 1024 * 1024;
    private const int readBufferBytes = 81920;
    private const int maxRequests = 6;
    private const string tokenHeader = "Authorization";
    private const string token = "Bearer token-one";
    private const string retryAfterHeader = "Retry-After";
    private const string maxRetryAfter = "300";
    private const int testTimeoutMs = 10_000;
    private const int maxAttempts = 3;
    private const string locationHeader = "Location";

    private static readonly TimeSpan attemptDeadline = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan failurePenalty = TimeSpan.FromSeconds(10);
    private static readonly Uri start = new("https://jobs.example.test/feed");
    private static readonly byte[] page = "{\"ads\":[]}"u8.ToArray();

    [Fact]
    public async Task ShouldStopReading_WhenBodyPassesTenMebibytes() {
        LargeBodyStream body = new(hostileBodyBytes);
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamContent(body),
        });

        await Assert.ThrowsAsync<BodyTooLargeException>(() => GetAsync(handler));

        Assert.InRange(body.Served, tenMebibytes, tenMebibytes + readBufferBytes);
    }

    [Theory]
    [InlineData("http://10.0.0.5/admin")]
    [InlineData("http://[::1]/admin")]
    public async Task ShouldRefuseRedirect_WhenTargetIsPrivateAddress(string location) {
        FakeSourceHandler handler = new(_ => Redirect(location));

        await Assert.ThrowsAsync<RefusedRequestException>(() => GetAsync(handler));

        Assert.Equal([start], handler.Urls);
    }

    [Fact]
    public async Task ShouldFollowRedirect_WhenLocationIsRelative() {
        FakeSourceHandler handler = new(index => index == 0 ? Redirect("/feed/2") : Ok());

        SourceResponse response = await GetAsync(handler);

        Assert.Equal(
            (new Uri("https://jobs.example.test/feed/2"), Convert.ToHexString(page)),
            (response.Url, Convert.ToHexString(response.Body)));
    }

    [Fact]
    public async Task ShouldRefuseRedirect_WhenChainPassesFiveHops() {
        FakeSourceHandler handler = new(index => Redirect($"/feed/{index + 1}"));

        await Assert.ThrowsAsync<RefusedRequestException>(() => GetAsync(handler));

        Assert.Equal(maxRequests, handler.Urls.Count);
    }

    [Fact]
    public async Task ShouldNotRetry_WhenSchemeIsUnsupported() {
        FakeSourceHandler handler = new(_ => Redirect("file:///etc/passwd"));

        await Assert.ThrowsAsync<RefusedRequestException>(() => GetAsync(handler));

        Assert.Single(handler.Urls);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task ShouldRetry_WhenStatusIsTransient(HttpStatusCode status) {
        FakeSourceHandler handler = new(index => index == 0 ? new HttpResponseMessage(status) : Ok());

        SourceResponse response = await GetAsync(handler);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Fact]
    public async Task ShouldReturnFailure_WhenThreeAttemptsFail() {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        SourceResponse response = await GetAsync(handler);

        Assert.Equal((HttpStatusCode.ServiceUnavailable, 3), (response.Status, handler.Urls.Count));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.NotImplemented)]
    public async Task ShouldNotRetry_WhenStatusIsFinal(HttpStatusCode status) {
        FakeSourceHandler handler = new(_ => new HttpResponseMessage(status));

        SourceResponse response = await GetAsync(handler);

        Assert.Equal((status, 1), (response.Status, handler.Urls.Count));
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.NameResolutionError)]
    public async Task ShouldRetry_WhenConnectionFails(HttpRequestError error) {
        FakeSourceHandler handler = new(index => index == 0 ? throw new HttpRequestException(error) : Ok());

        SourceResponse response = await GetAsync(handler);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Theory]
    [InlineData(HttpRequestError.HttpProtocolError)]
    [InlineData(HttpRequestError.InvalidResponse)]
    [InlineData(HttpRequestError.SecureConnectionError)]
    [InlineData(HttpRequestError.ResponseEnded)]
    public async Task ShouldNotRetry_WhenErrorIsPermanent(HttpRequestError error) {
        FakeSourceHandler handler = new(_ => throw new HttpRequestException(error));

        await Assert.ThrowsAsync<HttpRequestException>(() => GetAsync(handler));

        Assert.Single(handler.Urls);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldThrow_WhenConnectionFailsThreeTimes() {
        FakeSourceHandler handler = new(_ => throw new HttpRequestException(HttpRequestError.ConnectionError));
        using HttpClient client = new(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => SourceRequests.GetAsync(
            client, start, new SourceRequestOptions(), TestContext.Current.CancellationToken));

        Assert.Equal(maxAttempts, handler.Urls.Count);
    }

    [Fact]
    public async Task ShouldNotRetry_WhenProtocolErrorWrapsIOException() {
        FakeSourceHandler handler = new(_ => throw new HttpRequestException(
            HttpRequestError.HttpProtocolError, null, new IOException("bad frame")));

        await Assert.ThrowsAsync<HttpRequestException>(() => GetAsync(handler));

        Assert.Single(handler.Urls);
    }

    [Fact]
    public async Task ShouldNotRetry_WhenConnectIsRefused() {
        FakeSourceHandler handler = new(_ => throw new HttpRequestException(
            HttpRequestError.ConnectionError, null, new RefusedRequestException("private address")));

        await Assert.ThrowsAsync<HttpRequestException>(() => GetAsync(handler));

        Assert.Single(handler.Urls);
    }

    [Fact]
    public async Task ShouldRetry_WhenHeaderReadTimesOut() {
        FakeSourceHandler handler = new(index => index == 0
            ? throw new HttpRequestException(HttpRequestError.Unknown, null, new IOException("no bytes"))
            : Ok());

        SourceResponse response = await GetAsync(handler);

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Fact]
    public async Task ShouldRetry_WhenBodyReadFails() {
        FakeSourceHandler handler = new(index => index == 0 ? BrokenBody() : Ok());

        SourceResponse response = await GetAsync(handler);

        Assert.Equal(page, response.Body);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldRetry_WhenConnectTimesOut() {
        FakeTimeProvider time = new();
        FakeSourceHandler inner = new(index => index == 0 ? throw ConnectTimeout() : Ok());
        using HttpClient client = LimitedClient(inner, time);
        Task<SourceResponse> response =
            SourceRequests.GetAsync(client, start, new SourceRequestOptions(), TestContext.Current.CancellationToken);

        time.Advance(failurePenalty);

        Assert.Equal(HttpStatusCode.OK, (await response).Status);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldHoldRetry_WhenBodyReadFails() {
        FakeTimeProvider time = new();
        FakeSourceHandler inner = new(index => index == 0 ? BrokenBody() : Ok());
        using HttpClient client = LimitedClient(inner, time);

        Task<SourceResponse> response =
            SourceRequests.GetAsync(client, start, new SourceRequestOptions(), TestContext.Current.CancellationToken);

        Assert.Equal((false, true), await LimiterTiming.StateAroundAsync(time, response, failurePenalty));
    }

    [Fact]
    public async Task ShouldForwardHeaders_WhenRedirectStaysOnOrigin() {
        FakeSourceHandler handler = new(index => index == 0 ? Redirect("/feed/2") : Ok());

        await GetWithTokenAsync(handler);

        Assert.True(handler.Headers[1].ContainsKey(tokenHeader));
    }

    [Theory]
    [InlineData("https://other.example.test/feed")]
    [InlineData("http://jobs.example.test/feed")]
    public async Task ShouldDropHeaders_WhenRedirectLeavesOrigin(string location) {
        FakeSourceHandler handler = new(index => index == 0 ? Redirect(location) : Ok());

        await GetWithTokenAsync(handler);

        Assert.False(handler.Headers[1].ContainsKey(tokenHeader));
    }

    [Theory]
    [InlineData("//")]
    [InlineData("")]
    public async Task ShouldRefuseRedirect_WhenLocationIsUnreadable(string location) {
        FakeSourceHandler handler = new(_ => RawRedirect(location));

        await Assert.ThrowsAsync<RefusedRequestException>(() => GetAsync(handler));

        Assert.Single(handler.Urls);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldThrowIOException_WhenAttemptPassesDeadline() {
        FakeTimeProvider time = new();
        using SemaphoreSlim requested = new(0);
        FakeSourceHandler handler = new(_ => {
            requested.Release();
            return OpenBody();
        });
        using HttpClient client = new(handler);
        Task<SourceResponse> response = SourceRequests.GetAsync(
            client, start, new SourceRequestOptions { Time = time }, TestContext.Current.CancellationToken);

        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            await requested.WaitAsync(TestContext.Current.CancellationToken);
            time.Advance(attemptDeadline);
        }

        await Assert.ThrowsAsync<IOException>(() => response);
    }

    [Fact(Timeout = testTimeoutMs)]
    public async Task ShouldEndRetryWait_WhenCancelled() {
        FakeTimeProvider time = new();
        FakeSourceHandler inner = new(_ => RateLimited());
        using HttpClient client = new(new HostLimitHandler(new HostLimiter(time), time) { InnerHandler = inner });
        using CancellationTokenSource cancel =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<SourceResponse> response = SourceRequests.GetAsync(client, start, new SourceRequestOptions(), cancel.Token);

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response);
    }

    private static async Task<SourceResponse> GetAsync(FakeSourceHandler handler) {
        using HttpClient client = new(handler);
        return await SourceRequests.GetAsync(client, start, new SourceRequestOptions(), TestContext.Current.CancellationToken);
    }

    private static async Task GetWithTokenAsync(FakeSourceHandler handler) {
        SourceRequestOptions options = new() { Headers = new Dictionary<string, string> { [tokenHeader] = token } };
        using HttpClient client = new(handler);
        await SourceRequests.GetAsync(client, start, options, TestContext.Current.CancellationToken);
    }

    private static HttpClient LimitedClient(FakeSourceHandler inner, FakeTimeProvider time) =>
        new(new HostLimitHandler(new HostLimiter(time), time) { InnerHandler = inner });

    private static TaskCanceledException ConnectTimeout() => new("connect timed out", new TimeoutException());

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new ByteArrayContent(page) };

    private static HttpResponseMessage Redirect(string location) {
        HttpResponseMessage response = new(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static HttpResponseMessage RawRedirect(string location) {
        HttpResponseMessage response = new(HttpStatusCode.Found);
        response.Headers.TryAddWithoutValidation(locationHeader, location);
        return response;
    }

    private static HttpResponseMessage OpenBody() =>
        new(HttpStatusCode.OK) { Content = new StreamContent(new Pipe().Reader.AsStream()) };

    private static HttpResponseMessage RateLimited() {
        HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
        response.Headers.Add(retryAfterHeader, maxRetryAfter);
        return response;
    }

    private static HttpResponseMessage BrokenBody() {
        Pipe pipe = new();
        pipe.Writer.Complete(new IOException("connection reset"));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(pipe.Reader.AsStream()) };
    }
}
