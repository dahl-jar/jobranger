using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Jobranger.Contracts;
using Jobranger.Contracts.Envelopes;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class BrowserHub : IAsyncDisposable {
    private const string claimRoute = "pair/claim";
    private const string hubRoute = "hub";
    private const string presenceMethod = "Presence";
    private const string sendMethod = "Send";
    private const string receiveMethod = "Receive";
    private const string originHeader = "Origin";
    private const string cliOnlineField = "cli_online";
    private const string tokenQuery = "access_token";
    private const string versionField = "v";
    private const string codeField = "code";
    private const string handleField = "handle";
    private const string jsonType = "application/json";

    private static readonly TimeSpan wait = TimeSpan.FromSeconds(15);

    private readonly HubConnection connection;
    private readonly Channel<bool> presence = Channel.CreateUnbounded<bool>();
    private readonly Channel<Envelope> received = Channel.CreateUnbounded<Envelope>();

    private BrowserHub(Uri relay, string handle) {
        connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(relay, $"{hubRoute}?{tokenQuery}={Uri.EscapeDataString(handle)}"),
                HttpTransportType.WebSockets,
                options => {
                    options.SkipNegotiation = true;
                    options.Headers[originHeader] = RelayProcess.SiteOrigin;
                })
            .AddJsonProtocol(json => {
                json.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, ContractsJsonContext.Default);
                json.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, RelayTestJsonContext.Default);
            })
            .Build();
        connection.On<JsonElement>(presenceMethod, message =>
            presence.Writer.TryWrite(message.GetProperty(cliOnlineField).GetBoolean()));
        connection.On<Envelope>(receiveMethod, envelope => received.Writer.TryWrite(envelope));
    }

    public static async Task<string> ClaimAsync(Uri relay, string code) {
        using HttpClient http = new() { BaseAddress = relay };
        JsonObject body = new() { [versionField] = 1, [codeField] = code };
        using StringContent content = new(body.ToJsonString(), System.Text.Encoding.UTF8, jsonType);
        using HttpResponseMessage claimed = await http.PostAsync(claimRoute, content, TestContext.Current.CancellationToken);
        claimed.EnsureSuccessStatusCode();
        using JsonDocument session = JsonDocument.Parse(
            await claimed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return session.RootElement.GetProperty(handleField).GetString()!;
    }

    public static async Task<BrowserHub> ConnectAsync(Uri relay, string handle) {
        BrowserHub browser = new(relay, handle);
        await browser.connection.StartAsync(TestContext.Current.CancellationToken);
        return browser;
    }

    public async Task WaitForCliAsync() {
        while (!await presence.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
                   .WaitAsync(wait, TestContext.Current.CancellationToken)) {
        }
    }

    public async Task<Envelope> NextReceivedAsync() =>
        await received.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(wait, TestContext.Current.CancellationToken);

    public Task SendAsync(Envelope envelope) =>
        connection.InvokeAsync(sendMethod, envelope, TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
