using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Tests.Http;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Features.Delivery;

public sealed class ResendSenderTests {
    private const string idempotencyKey = "key-1";
    private const int maxReplyBytes = 64 * 1024;

    private readonly FakeTimeProvider time = AdFixtures.Clock();

    [Fact]
    public async Task ShouldPostDigest_WhenKeyIsGiven() {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);

        SendFailure? failure = await SendAsync(handler);

        Assert.Null(failure);
        SentRequest sent = handler.Last;
        JsonNode body = JsonNode.Parse(sent.Text)!;
        Assert.Equal("https://api.resend.com/emails", sent.Uri.AbsoluteUri);
        Assert.Equal("Bearer " + DeliveryFixtures.ResendKey, sent.Header("Authorization"));
        Assert.Equal(idempotencyKey, sent.Header("Idempotency-Key"));
        Assert.Equal("close", sent.Header("Connection"));
        Assert.Equal(DeliveryFixtures.Addresses.Sender, (string?)body["from"]);
        Assert.Equal(DeliveryFixtures.Address, (string?)body["to"]![0]);
        Assert.Equal(DeliveryFixtures.Rendered.Subject, (string?)body["subject"]);
        Assert.Equal(DeliveryFixtures.Rendered.Html, (string?)body["html"]);
        Assert.Equal(DeliveryFixtures.Rendered.Text, (string?)body["text"]);
    }

    [Fact]
    public async Task ShouldOpenNewConnection_WhenEachEmailIsSent() {
        int created = 0;
        ResendSender sender = Sender(() => {
            created++;
            return FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);
        });

        await SendWithAsync(sender, TestContext.Current.CancellationToken);
        await SendWithAsync(sender, TestContext.Current.CancellationToken);

        Assert.Equal(2, created);
    }

    [Theory]
    [InlineData(202)]
    [InlineData(299)]
    public async Task ShouldCountAsSent_WhenStatusIsSuccess(int status) {
        SendFailure? failure = await SendAsync(FakeApiHandler.Json(status, DeliveryFixtures.EmptyJson));

        Assert.Null(failure);
    }

    [Theory]
    [InlineData("re_a b")]
    [InlineData("re_a~b")]
    public async Task ShouldSendKey_WhenItHasOnlyPrintableAscii(string key) {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);

        SendFailure? failure = await SendWithAsync(Sender(() => handler, key), TestContext.Current.CancellationToken);

        Assert.Null(failure);
        Assert.Equal("Bearer " + key, handler.Last.Header("Authorization"));
    }

    [Theory]
    [InlineData(300, DeliveryFixtures.EmptyJson, nameof(SendFailureKind.ResendRedirect), "Resend answered with a redirect (300), so the email was not sent.")]
    [InlineData(307, DeliveryFixtures.EmptyJson, nameof(SendFailureKind.ResendRedirect), "Resend answered with a redirect (307), so the email was not sent.")]
    [InlineData(399, DeliveryFixtures.EmptyJson, nameof(SendFailureKind.ResendRedirect), "Resend answered with a redirect (399), so the email was not sent.")]
    [InlineData(400, DeliveryFixtures.EmptyJson, nameof(SendFailureKind.ResendStatus), "Resend answered 400.")]
    [InlineData(401, DeliveryFixtures.EmptyJson, nameof(SendFailureKind.ResendKey), "Resend refused the key.")]
    [InlineData(403, DeliveryFixtures.EmptyJson, nameof(SendFailureKind.ResendKey), "Resend refused the key.")]
    [InlineData(422, "{\"message\":\"bad sender\"}", nameof(SendFailureKind.ResendRejected), "bad sender")]
    [InlineData(422, "not json", nameof(SendFailureKind.ResendStatus), "Resend answered 422.")]
    [InlineData(500, DeliveryFixtures.EmptyJson, nameof(SendFailureKind.ResendStatus), "Resend answered 500.")]
    public async Task ShouldExplainFailure_WhenResendRefuses(int status, string body, string kind, string reason) {
        SendFailure? failure = await SendAsync(FakeApiHandler.Json(status, body));

        Assert.Equal(Enum.Parse<SendFailureKind>(kind), failure?.Kind);
        Assert.Equal(status, failure?.Status);
        Assert.Equal(reason, failure?.Reason);
    }

    [Fact]
    public async Task ShouldReportStatus_WhenRejectionBodyIsTooLong() {
        string body = new('x', maxReplyBytes + 1);

        SendFailure? failure = await SendAsync(FakeApiHandler.Json(422, body));

        Assert.Equal(SendFailureKind.ResendStatus, failure?.Kind);
        Assert.Equal(422, failure?.Status);
    }

    [Fact]
    public async Task ShouldReportUnreachable_WhenConnectionFails() {
        SendFailure? failure = await SendAsync(FakeApiHandler.Refusing());

        Assert.Equal(SendFailureKind.ResendUnreachable, failure?.Kind);
    }

    [Fact]
    public void ShouldCleanMessage_WhenResendRejects() {
        string text = "bad\u0007  sender\n" + new string('x', 300);
        byte[] body = Encoding.UTF8.GetBytes(new JsonObject { ["message"] = text }.ToJsonString());

        string message = ResendReply.Message(body);

        Assert.Equal(200, message.Length);
        Assert.StartsWith("bad sender xxx", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldCutMessageByCodePoints_WhenItHasEmoji() {
        string text = string.Concat(Enumerable.Repeat("\U0001F600", 201));
        byte[] body = Encoding.UTF8.GetBytes(new JsonObject { ["message"] = text }.ToJsonString());

        string message = ResendReply.Message(body);

        Assert.Equal(200, CodePoints.Count(message));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[1]")]
    [InlineData("{\"message\":5}")]
    [InlineData("{\"message\":\"\\ud83d\"}")]
    [InlineData("{\"message\":\"\u00FF\"}")]
    public void ShouldReturnEmptyMessage_WhenReplyHasNoText(string body) {
        string message = ResendReply.Message(Encoding.Latin1.GetBytes(body));

        Assert.Empty(message);
    }

    [Theory]
    [InlineData("re_é")]
    [InlineData("re_\n")]
    public async Task ShouldRefuseKey_WhenKeyHasHiddenCharacters(string key) {
        FakeApiHandler handler = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);

        SendFailure? failure = await SendWithAsync(Sender(() => handler, key), TestContext.Current.CancellationToken);

        Assert.Equal(SendFailureKind.UnusableResendKey, failure?.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ShouldReportTimeout_WhenResendDoesNotAnswer() {
        DateTimeOffset started = time.GetUtcNow();

        SendFailure? failure = await ApiClock.RunAsync(time, SendAsync(FakeApiHandler.Hanging()));

        Assert.Equal(SendFailureKind.ResendTimeout, failure?.Kind);
        DeliveryFixtures.AssertOneOperationPassed(time, started);
    }

    [Fact]
    public async Task ShouldReportTimeout_WhenRejectionBodyStalls() {
        Pipe pipe = new();
        FakeApiHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableContent) {
            Content = new StreamContent(pipe.Reader.AsStream()),
        }));

        SendFailure? failure = await ApiClock.RunAsync(time, SendAsync(handler));

        Assert.Equal(SendFailureKind.ResendTimeout, failure?.Kind);
    }

    [Fact]
    public async Task ShouldStopSend_WhenAnswerDripsPast120Seconds() {
        FakeApiHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableContent) {
            Content = new StreamContent(new DripStream(time, DeliveryFixtures.SlowAnswer)),
        }));

        DateTimeOffset started = time.GetUtcNow();

        SendFailure? failure = await SendAsync(handler);

        Assert.Equal(SendFailureKind.ResendTooSlow, failure?.Kind);
        Assert.Contains("120 s", failure?.Reason, StringComparison.Ordinal);
        Assert.InRange(time.GetUtcNow() - started, SendLimits.Deadline, SendLimits.Deadline + SendLimits.Operation);
    }

    [Fact]
    public async Task ShouldCountAsSent_WhenBodyStalls() {
        using StalledBodyHandler handler = new([]);

        SendFailure? failure = await SendWithAsync(Sender(() => handler), TestContext.Current.CancellationToken);

        Assert.Null(failure);
    }

    [Fact]
    public async Task ShouldNotFollowRedirect_WhenProductionHandlerGets307() {
        await using RawHttpServer server = new(
            "HTTP/1.1 307 Temporary Redirect\r\nLocation: http://127.0.0.1:1/next\r\nContent-Length: 0\r\n\r\n");
        using HttpClient client = new(DeliveryTransport.Production.CreateResendHandler());

        using HttpResponseMessage response = await client.GetAsync(server.Url("/"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }

    [Fact]
    public async Task ShouldThrowCanceled_WhenCallerCancels() {
        using CancellationTokenSource caller = new();
        FakeApiHandler handler = FakeApiHandler.Hanging();
        Task<SendFailure?> sending = SendWithAsync(Sender(() => handler), caller.Token);
        await handler.ArrivedAsync(TestWaits.Step);

        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
    }

    private ResendSender Sender(Func<HttpMessageHandler> create, string apiKey = DeliveryFixtures.ResendKey) =>
        new(apiKey, idempotencyKey, DeliveryTransport.Production with { CreateResendHandler = create }, new EnglishMessages(), time);

    private Task<SendFailure?> SendAsync(FakeApiHandler handler) =>
        SendWithAsync(Sender(() => handler), TestContext.Current.CancellationToken);

    private static Task<SendFailure?> SendWithAsync(ResendSender sender, CancellationToken cancellationToken) =>
        sender.SendAsync(DeliveryFixtures.Rendered, DeliveryFixtures.Addresses, cancellationToken)
            .WaitAsync(TestWaits.Step, TestContext.Current.CancellationToken);
}
