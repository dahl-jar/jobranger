using System.Globalization;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Http;

public sealed class ApiSenderTests {
    private const string target = "https://api.example.test/v1/messages";
    private const string okBody = "{}";
    private const int retries = 4;

    private static readonly TimeSpan settle = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan early = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan rest = TimeSpan.FromMilliseconds(1);

    private readonly FakeTimeProvider time = ApiClock.Create();

    [Fact]
    public async Task ShouldSendFiveTimes_WhenServerKeepsFailing() {
        using FakeApiHandler handler = FakeApiHandler.Json(500, okBody);
        using ApiSender sender = Create(handler, retries);

        ApiReply reply = await ApiClock.RunAsync(time, Send(sender));

        Assert.Equal((500, retries + 1), (reply.Status, handler.Requests.Count));
    }

    [Fact]
    public async Task ShouldSendOnce_WhenStatusIsBadRequest() {
        using FakeApiHandler handler = FakeApiHandler.Json(400, okBody);
        using ApiSender sender = Create(handler, retries);

        await ApiClock.RunAsync(time, Send(sender));

        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(409)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task ShouldRetry_WhenStatusIsRetryable(int status) {
        using FakeApiHandler handler = FakeApiHandler.Sequence(
            () => FakeApiHandler.Response(status, okBody), () => FakeApiHandler.Response(200, okBody));
        using ApiSender sender = Create(handler, retries);

        ApiReply reply = await ApiClock.RunAsync(time, Send(sender));

        Assert.Equal(200, reply.Status);
    }

    [Fact]
    public async Task ShouldSendOnce_WhenRetriesAreOff() {
        using FakeApiHandler handler = FakeApiHandler.Json(500, okBody);
        using ApiSender sender = Create(handler, 0);

        await ApiClock.RunAsync(time, Send(sender));

        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("30", 30)]
    [InlineData("60", 60)]
    [InlineData("120", 120)]
    [InlineData("2.5", 2.5)]
    public async Task ShouldWaitRetryAfter_WhenServerSetsIt(string header, double seconds) {
        using FakeApiHandler handler = RetryThenOk(("retry-after", header));
        using ApiSender sender = Create(handler, retries);
        Task<ApiReply> sending = Send(sender);
        await handler.ArrivedAsync(settle);

        (bool tooEarly, bool onTime) = await NextArrivalAsync(handler, TimeSpan.FromSeconds(seconds));

        Assert.Equal((false, true), (tooEarly, onTime));
        await sending;
    }

    [Fact]
    public async Task ShouldPreferRetryAfterMs_WhenServerSetsBoth() {
        using FakeApiHandler handler = RetryThenOk(("retry-after-ms", "1500"), ("retry-after", "30"));
        using ApiSender sender = Create(handler, retries);
        Task<ApiReply> sending = Send(sender);
        await handler.ArrivedAsync(settle);

        (bool tooEarly, bool onTime) = await NextArrivalAsync(handler, TimeSpan.FromSeconds(1.5));

        Assert.Equal((false, true), (tooEarly, onTime));
        await sending;
    }

    [Fact]
    public async Task ShouldWaitUntilDate_WhenRetryAfterIsDate() {
        string date = time.GetUtcNow().AddSeconds(20).ToString("r", CultureInfo.InvariantCulture);
        using FakeApiHandler handler = RetryThenOk(("retry-after", date));
        using ApiSender sender = Create(handler, retries);
        Task<ApiReply> sending = Send(sender);
        await handler.ArrivedAsync(settle);

        (bool tooEarly, bool onTime) = await NextArrivalAsync(handler, TimeSpan.FromSeconds(20));

        Assert.Equal((false, true), (tooEarly, onTime));
        await sending;
    }

    [Theory]
    [InlineData("200", 200)]
    [InlineData("9999999", 4294967)]
    public async Task ShouldWaitLongRetryAfter_WhenSdkIsAnthropic(string header, double seconds) {
        using FakeApiHandler handler = RetryThenOk(("retry-after", header));
        using ApiSender sender = Create(handler, retries, SdkRules.Anthropic);
        Task<ApiReply> sending = Send(sender);
        await handler.ArrivedAsync(settle);

        (bool tooEarly, bool onTime) = await NextArrivalAsync(handler, TimeSpan.FromSeconds(seconds));

        Assert.Equal((false, true), (tooEarly, onTime));
        await sending;
    }

    [Theory]
    [InlineData("121", "")]
    [InlineData("200", "true")]
    public async Task ShouldReturnReply_WhenOpenAIRetryAfterPassesTwoMinutes(string retryAfter, string shouldRetry) {
        using FakeApiHandler handler = FakeApiHandler.Json(
            429, okBody, ("retry-after", retryAfter), ("x-should-retry", shouldRetry));
        using ApiSender sender = Create(handler, retries);

        ApiReply reply = await ApiClock.RunAsync(time, Send(sender));

        Assert.Equal((429, 1), (reply.Status, handler.Requests.Count));
    }

    [Theory]
    [InlineData(400, "true", 5)]
    [InlineData(500, "false", 1)]
    [InlineData(200, "true", 1)]
    public async Task ShouldFollowServer_WhenShouldRetryHeaderIsSet(int status, string header, int requests) {
        using FakeApiHandler handler = FakeApiHandler.Json(status, okBody, ("x-should-retry", header));
        using ApiSender sender = Create(handler, retries);

        await ApiClock.RunAsync(time, Send(sender));

        Assert.Equal(requests, handler.Requests.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    public async Task ShouldDoubleWait_WhenRetryAfterIsMissingOrZero(string? retryAfter) {
        (string, string)[] headers = retryAfter is null ? [] : [("retry-after", retryAfter)];
        using FakeApiHandler handler = FakeApiHandler.Json(503, okBody, headers);
        using ApiSender sender = Create(handler, retries);

        List<(bool, bool)> waits = await WaitsAsync(handler, sender, 0.5, 1, 2, 4);

        Assert.All(waits, wait => Assert.Equal((false, true), wait));
    }

    [Fact]
    public async Task ShouldShortenWait_WhenJitterIsDrawn() {
        using FakeApiHandler handler = FakeApiHandler.Json(503, okBody);
        using ApiSender sender = Create(handler, retries, jitter: 0.5);

        List<(bool, bool)> waits = await WaitsAsync(handler, sender, 0.4375, 0.875, 1.75, 3.5);

        Assert.All(waits, wait => Assert.Equal((false, true), wait));
    }

    [Fact]
    public async Task ShouldCapWaitAtEightSeconds_WhenRetriesRunLong() {
        using FakeApiHandler handler = FakeApiHandler.Json(503, okBody);
        using ApiSender sender = Create(handler, 6);

        List<(bool, bool)> waits = await WaitsAsync(handler, sender, 0.5, 1, 2, 4, 8, 8);

        Assert.All(waits, wait => Assert.Equal((false, true), wait));
    }

    [Fact]
    public async Task ShouldThrowConnectionError_WhenHostRefuses() {
        using FakeApiHandler handler = FakeApiHandler.Refusing();
        using ApiSender sender = Create(handler, retries);

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ApiClock.RunAsync(time, Send(sender)));

        Assert.Equal((ApiErrorKind.Connection, "Connection error.", retries + 1),
            (error.Kind, error.Message, handler.Requests.Count));
    }

    [Theory]
    [InlineData(false, "Request timed out.")]
    [InlineData(true, ProviderFixtures.AnthropicTimeout)]
    public async Task ShouldThrowTimeout_WhenProviderNeverAnswers(bool anthropic, string message) {
        using FakeApiHandler handler = FakeApiHandler.Hanging();
        using ApiSender sender = Create(handler, retries, anthropic ? SdkRules.Anthropic : SdkRules.OpenAI);

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ApiClock.RunAsync(time, Send(sender)));

        Assert.Equal((ApiErrorKind.Connection, message), (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldThrowCanceled_WhenCallerCancelsDuringAttempt() {
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        using FakeApiHandler handler = FakeApiHandler.Hanging();
        using ApiSender sender = Create(handler, 0);
        Task<ApiReply> sending = SendCancelable(sender, cancel.Token);
        await handler.ArrivedAsync(settle);

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
    }

    [Fact]
    public async Task ShouldStopRetrying_WhenCallerCancelsDuringBackoff() {
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        using FakeApiHandler handler = FakeApiHandler.Json(500, okBody);
        using ApiSender sender = Create(handler, retries);
        Task<ApiReply> sending = SendCancelable(sender, cancel.Token);
        await handler.ArrivedAsync(settle);

        await cancel.CancelAsync();
        time.Advance(TimeSpan.FromSeconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ShouldWaitFullTimeout_WhenProviderIsSlow() {
        using FakeApiHandler handler = FakeApiHandler.Hanging();
        using ApiSender sender = Create(handler, 0);
        Task<ApiReply> sending = Send(sender);
        await handler.ArrivedAsync(settle);

        time.Advance(TimeSpan.FromSeconds(120) - rest);
        bool tooEarly = await Task.WhenAny(sending, Task.Delay(early, TestContext.Current.CancellationToken)) == sending;
        time.Advance(rest);

        Assert.False(tooEarly);
        await Assert.ThrowsAsync<ApiException>(() => sending.WaitAsync(settle, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ShouldThrowServerError_WhenReplyPassesCap() {
        using FakeApiHandler handler = new((request, cancellationToken) => Task.FromResult(
            new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new StreamContent(new LargeBodyStream(ApiSender.MaxBodyBytes + 1)),
            }));
        using ApiSender sender = Create(handler, retries);

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ApiClock.RunAsync(time, Send(sender)));

        Assert.Equal((ApiErrorKind.Server, 1), (error.Kind, handler.Requests.Count));
    }

    [Fact]
    public async Task ShouldKeepRetryAfter_WhenReplyCarriesIt() {
        using FakeApiHandler handler = FakeApiHandler.Json(400, okBody, ("retry-after", "7"));
        using ApiSender sender = Create(handler, retries);

        ApiReply reply = await ApiClock.RunAsync(time, Send(sender));

        Assert.Equal(TimeSpan.FromSeconds(7), reply.RetryAfter);
    }

    private static FakeApiHandler RetryThenOk(params (string Name, string Value)[] headers) =>
        FakeApiHandler.Sequence(
            () => FakeApiHandler.Response(429, okBody, headers),
            () => FakeApiHandler.Response(200, okBody));

    private static Task<ApiReply> Send(ApiSender sender) =>
        SendCancelable(sender, TestContext.Current.CancellationToken);

    private static Task<ApiReply> SendCancelable(ApiSender sender, CancellationToken cancellationToken) =>
        sender.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, target), cancellationToken);

    private ApiSender Create(FakeApiHandler handler, int retryCount, SdkRules? rules = null, double jitter = 0) =>
        new(handler, time, retryCount, rules ?? SdkRules.OpenAI, new FixedRandom(jitter));

    private async Task<List<(bool, bool)>> WaitsAsync(
        FakeApiHandler handler, ApiSender sender, params double[] seconds) {
        Task<ApiReply> sending = Send(sender);
        await handler.ArrivedAsync(settle);
        List<(bool, bool)> waits = [];
        foreach (double wait in seconds) {
            waits.Add(await NextArrivalAsync(handler, TimeSpan.FromSeconds(wait)));
        }
        await ApiClock.RunAsync(time, sending);
        return waits;
    }

    private async Task<(bool TooEarly, bool OnTime)> NextArrivalAsync(FakeApiHandler handler, TimeSpan due) {
        time.Advance(due - rest);
        bool tooEarly = await handler.ArrivedAsync(early);
        time.Advance(rest);
        bool onTime = tooEarly || await handler.ArrivedAsync(settle);
        return (tooEarly, onTime);
    }
}
