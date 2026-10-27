using System.Buffers.Text;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Jobranger.Cli.Features.Link;
using Jobranger.Contracts.Channels;
using Jobranger.Contracts.Envelopes;
using Jobranger.Contracts.Proofs;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeRelay : HttpMessageHandler, IRelayConnector {
    public const string Code = "BCDF-GHJK";
    public const string PairHandle = "pair-handle";
    public const string CliHandle = "cli-handle";
    public const string NonceRoute = "/pair/nonce";
    public const string StartRoute = "/pair/start";
    public const string SessionRoute = "/session/cli";

    private const string problemType = "application/problem+json";

    private readonly TimeProvider time;
    private readonly Channel<Envelope> fromCli = System.Threading.Channels.Channel.CreateUnbounded<Envelope>();
    private readonly Channel<RelayEvent> toCli = System.Threading.Channels.Channel.CreateUnbounded<RelayEvent>();
    private int nonces;

    public FakeRelay(TimeProvider time) {
        this.time = time;
    }

    public static Uri Address { get; } = new("https://relay.example.test");

    public List<string> Routes { get; } = [];

    public List<JsonObject> Bodies { get; } = [];

    public List<string> Handles { get; } = [];

    public Dictionary<string, HttpResponseMessage> Failures { get; } = [];

    public Exception? ConnectFailure { get; set; }

    public Exception? SendFailure { get; set; }

    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public string Channel { get; private set; } = "";

    public string? AnswerCode { get; set; }

    public HttpClient Client() => new(this);

    public Task<IRelayConnection> ConnectAsync(string handle, CancellationToken cancellationToken) {
        Handles.Add(handle);
        if (ConnectFailure is not null) {
            throw ConnectFailure;
        }
        return Task.FromResult<IRelayConnection>(new Connection(this));
    }

    public async Task<Envelope> NextFromCliAsync() =>
        await fromCli.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TestWaits.Step);

    public IReadOnlyList<Envelope> SentSoFar() {
        List<Envelope> sent = [];
        while (fromCli.Reader.TryRead(out Envelope? envelope)) {
            sent.Add(envelope);
        }
        return sent;
    }

    public void Hello(string sid, string step, long seq = 0) =>
        toCli.Writer.TryWrite(new RelayEvent.Received(EnvelopeFixtures.Hello(Channel, sid, step, seq)));

    public void Data(string sid, long seq, string ciphertext) =>
        toCli.Writer.TryWrite(new RelayEvent.Received(EnvelopeFixtures.Data(Channel, sid, seq, ciphertext)));

    public void CloseSessions(params string[] sids) => toCli.Writer.TryWrite(new RelayEvent.SessionsClosed(sids));

    public void Disconnect() => toCli.Writer.TryWrite(new RelayEvent.Disconnected());

    public void Replace() => toCli.Writer.TryWrite(new RelayEvent.Replaced());

    public static string Step(Envelope envelope) {
        using JsonDocument step = JsonDocument.Parse(Base64Url.DecodeFromChars(envelope.Ciphertext));
        return step.RootElement.GetProperty("t").GetString()!;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        string route = request.RequestUri!.AbsolutePath;
        Routes.Add(route);
        JsonObject body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
        Bodies.Add(body);
        if (Failures.TryGetValue(route, out HttpResponseMessage? failure)) {
            return failure;
        }
        return route switch {
            NonceRoute => Json(new JsonObject {
                ["nonce"] = $"nonce-{++nonces}",
                ["expiresAt"] = Later(CodeLifetime),
            }),
            StartRoute => Proven(body, OwnershipProof.PairStartPurpose) ?? Json(new JsonObject {
                ["code"] = AnswerCode ?? Code,
                ["expiresAt"] = Later(CodeLifetime),
                ["channel"] = Channel,
                ["handle"] = PairHandle,
                ["handleExpiresAt"] = Later(CodeLifetime),
            }),
            SessionRoute => Proven(body, OwnershipProof.SessionCliPurpose) ?? Json(new JsonObject {
                ["channel"] = Channel,
                ["handle"] = CliHandle,
                ["handleExpiresAt"] = Later(CodeLifetime),
            }),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    public static HttpResponseMessage Problem(HttpStatusCode status, string code) => new(status) {
        Content = new StringContent($"{{\"status\":{(int)status},\"code\":\"{code}\"}}", Encoding.UTF8, problemType),
    };

    private HttpResponseMessage? Proven(JsonObject body, string purpose) {
        byte[] publicKey = Base64Url.DecodeFromChars(body["publicKey"]!.GetValue<string>());
        byte[] signature = Base64Url.DecodeFromChars(body["signature"]!.GetValue<string>());
        if (!OwnershipProof.Verify(publicKey, purpose, body["nonce"]!.GetValue<string>(), signature)) {
            return Problem(HttpStatusCode.BadRequest, "invalid_signature");
        }
        Channel = ChannelId.FromPublicKey(publicKey).Value;
        return null;
    }

    private string Later(TimeSpan span) => (time.GetUtcNow() + span).ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static HttpResponseMessage Json(JsonObject body) => new(HttpStatusCode.OK) {
        Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
    };

    private sealed class Connection(FakeRelay relay) : IRelayConnection {
        public Task SendAsync(Envelope envelope, CancellationToken cancellationToken) {
            if (relay.SendFailure is not null) {
                throw relay.SendFailure;
            }
            relay.fromCli.Writer.TryWrite(envelope);
            return Task.CompletedTask;
        }

        public ValueTask<RelayEvent> ReceiveAsync(CancellationToken cancellationToken) =>
            relay.toCli.Reader.ReadAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
