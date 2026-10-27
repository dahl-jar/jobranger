using System.IO.Pipelines;
using System.Net;
using System.Text;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Contracts.Channels;
using Microsoft.Extensions.Time.Testing;
using NSec.Cryptography;

namespace Jobranger.Cli.Tests.Features.Link;

public sealed class RelayApiClientTests : IDisposable {
    private const int oversizedBytes = 70_000;

    private static readonly DateTimeOffset now = new(2026, 10, 25, 9, 30, 0, TimeSpan.Zero);

    private readonly FakeRelay relay = new(new FakeTimeProvider(now));
    private readonly HttpClient http;
    private readonly RelayApiClient client;
    private readonly Key signing = Key.Create(SignatureAlgorithm.Ed25519);

    public RelayApiClientTests() {
        http = relay.Client();
        client = new RelayApiClient(http, FakeRelay.Address);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string OwnChannel => ChannelId.FromPublicKey(signing.PublicKey.Export(KeyBlobFormat.RawPublicKey)).Value;

    public void Dispose() {
        signing.Dispose();
        http.Dispose();
        relay.Dispose();
    }

    [Fact]
    public async Task ShouldReturnCodeAndHandle_WhenPairingStarts() {
        PairingStart started = await client.StartPairingAsync(signing, Token);

        Assert.Equal(
            (FakeRelay.Code, FakeRelay.PairHandle, now.AddMinutes(10), OwnChannel),
            (started.Code, started.Handle, started.ExpiresAt, relay.Channel));
    }

    [Fact]
    public async Task ShouldAskNonceForOwnChannel_WhenPairingStarts() {
        await client.StartPairingAsync(signing, Token);

        Assert.Equal(
            ($"{FakeRelay.NonceRoute} {FakeRelay.StartRoute}", $"{{\"v\":1,\"channel\":\"{OwnChannel}\"}}"),
            (string.Join(' ', relay.Routes), relay.Bodies[0].ToJsonString()));
    }

    [Fact]
    public async Task ShouldPostNonceThenSessionCli_WhenHandleIsRequested() {
        string handle = await client.CliHandleAsync(signing, Token);

        Assert.Equal(
            (FakeRelay.CliHandle, $"{FakeRelay.NonceRoute} {FakeRelay.SessionRoute}"),
            (handle, string.Join(' ', relay.Routes)));
    }

    [Theory]
    [InlineData(FakeRelay.NonceRoute, HttpStatusCode.TooManyRequests, "rate_limited")]
    [InlineData(FakeRelay.StartRoute, HttpStatusCode.BadRequest, "invalid_signature")]
    public async Task ShouldThrowRejected_WhenRelayAnswersClientError(string route, HttpStatusCode status, string code) {
        relay.Failures[route] = FakeRelay.Problem(status, code);

        RelayRejectedException rejected =
            await Assert.ThrowsAsync<RelayRejectedException>(() => client.StartPairingAsync(signing, Token));

        Assert.Equal(((int)status, code), (rejected.Status, rejected.Code));
    }

    [Fact]
    public async Task ShouldThrowUnavailable_WhenRelayAnswersServerError() {
        relay.Failures[FakeRelay.NonceRoute] = new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = new StringContent("{\"nonce\":\"n\"}", Encoding.UTF8),
        };

        await Assert.ThrowsAsync<RelayUnavailableException>(() => client.StartPairingAsync(signing, Token));
    }

    [Fact]
    public async Task ShouldThrowRejectedWithUnknownCode_WhenClientErrorBodyIsNotJson() {
        relay.Failures[FakeRelay.NonceRoute] = new HttpResponseMessage(HttpStatusCode.Forbidden) {
            Content = new StringContent("<html>", Encoding.UTF8),
        };

        RelayRejectedException rejected =
            await Assert.ThrowsAsync<RelayRejectedException>(() => client.StartPairingAsync(signing, Token));

        Assert.Equal("unknown", rejected.Code);
    }

    [Fact]
    public async Task ShouldThrowCancelled_WhenCallerCancels() {
        using HttpClient cancelling = new(new ThrowingHandler(new TaskCanceledException("caller cancelled")));
        RelayApiClient cancelled = new(cancelling, FakeRelay.Address);
        using CancellationTokenSource stop = new();
        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.StartPairingAsync(signing, stop.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldThrowUnavailable_WhenTransportFails(bool timesOut) {
        using HttpClient broken = new(new ThrowingHandler(
            timesOut ? new TaskCanceledException("request timed out") : new HttpRequestException("connection refused")));
        RelayApiClient unreachable = new(broken, FakeRelay.Address);

        await Assert.ThrowsAsync<RelayUnavailableException>(() => unreachable.StartPairingAsync(signing, Token));
    }

    [Theory]
    [InlineData("BCDF-GHJ")]
    [InlineData("AEIO-UBCD")]
    [InlineData("bcdf-ghjk")]
    public async Task ShouldThrowUnavailable_WhenCodeIsMalformed(string code) {
        relay.AnswerCode = code;

        await Assert.ThrowsAsync<RelayUnavailableException>(() => client.StartPairingAsync(signing, Token));
    }

    [Fact]
    public async Task ShouldThrowUnavailable_WhenBodyIsTooLarge() {
        relay.Failures[FakeRelay.NonceRoute] = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent($"{{\"nonce\":\"{new string('a', oversizedBytes)}\"}}", Encoding.UTF8),
        };

        await Assert.ThrowsAsync<RelayUnavailableException>(() => client.StartPairingAsync(signing, Token));
    }

    [Fact]
    public async Task ShouldThrowUnavailable_WhenOversizedBodyNeverEnds() {
        using HttpClient stalled = new(new StalledOversizedHandler());
        RelayApiClient capped = new(stalled, FakeRelay.Address);

        Task<PairingStart> started = capped.StartPairingAsync(signing, Token).WaitAsync(TestWaits.Step, Token);

        await Assert.ThrowsAsync<RelayUnavailableException>(() => started);
    }

    [Theory]
    [InlineData("<html>")]
    [InlineData("[]")]
    public async Task ShouldThrowUnavailable_WhenBodyIsNotJsonObject(string body) {
        relay.Failures[FakeRelay.NonceRoute] = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(body, Encoding.UTF8),
        };

        await Assert.ThrowsAsync<RelayUnavailableException>(() => client.StartPairingAsync(signing, Token));
    }

    private sealed class ThrowingHandler(Exception failure) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw failure;
    }

    private sealed class StalledOversizedHandler : HttpMessageHandler {
        private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) {
            await pipe.Writer.WriteAsync(new byte[oversizedBytes], cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(pipe.Reader.AsStream()) };
        }
    }
}
