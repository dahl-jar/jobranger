using System.Collections.Concurrent;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Messaging.Envelopes;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Cli.Tests.Features.Link;

public sealed class RelayLinkTests : IAsyncLifetime {
    private const double jitter = 0.1;
    private const string replacedMethod = "Replaced";
    private const string notConnected = "relay link is not connected";

    private static readonly TimeSpan wait = TimeSpan.FromSeconds(15);

    private readonly RelayProcess relay = new();
    private readonly HttpClient http = new();
    private readonly LinkKeyPair keys = LinkKeys.Parse(LinkKeys.NewPair());
    private readonly ConcurrentQueue<string> handles = [];
    private readonly FakeLogger logger = new();
    private readonly CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
    private RelayApiClient api = null!;
    private RelayLink link = null!;
    private Task running = Task.CompletedTask;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        await relay.StartAsync();
        api = new RelayApiClient(http, relay.Address);
        link = new RelayLink(
            new HubConnector(relay.Address, TimeProvider.System), NextHandleAsync, TimeProvider.System, () => jitter, logger);
    }

    public async ValueTask DisposeAsync() {
        await stop.CancelAsync();
        try {
            await running;
        } finally {
            await relay.DisposeAsync();
            http.Dispose();
            keys.Dispose();
            stop.Dispose();
        }
    }

    [Fact]
    public async Task ShouldUseFreshHandle_WhenReconnecting() {
        await ReconnectAsync();

        Assert.True(handles.Count > 1);
    }

    [Fact]
    public async Task ShouldLeaveHandlesOutOfLogs_WhenReconnecting() {
        await ReconnectAsync();

        Assert.DoesNotContain(
            logger.Collector.GetSnapshot(),
            record => handles.Any(handle => record.Message.Contains(handle, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ShouldReportClosedSessions_WhenBrowserLeaves() {
        running = link.RunAsync(stop.Token);
        BrowserHub browser = await BrowserHub.ConnectAsync(relay.Address, await BrowserHandleAsync());
        await browser.WaitForCliAsync();
        await browser.SendAsync(EnvelopeFixtures.Hello(keys.Channel.Value, EnvelopeFixtures.Sid));
        await NextAsync<RelayEvent.Received>();

        await browser.DisposeAsync();

        RelayEvent.SessionsClosed closed = await NextAsync<RelayEvent.SessionsClosed>();
        Assert.Equal([EnvelopeFixtures.Sid], closed.Sids);
    }

    [Fact]
    public async Task ShouldStopReconnecting_WhenAnotherCliReplacesIt() {
        running = link.RunAsync(stop.Token);
        await using BrowserHub browser = await BrowserHub.ConnectAsync(relay.Address, await BrowserHandleAsync());
        await browser.WaitForCliAsync();
        HubConnector other = new(relay.Address, TimeProvider.System);

        await using IRelayConnection replacing =
            await other.ConnectAsync(await api.CliHandleAsync(keys.Signing, Token), Token);

        await running.WaitAsync(wait, Token);
        Assert.Single(handles);
    }

    [Fact]
    public async Task ShouldStopReconnecting_WhenCloseOvertakesReplaced() {
        TaskCompletionSource entered = new();
        TaskCompletionSource heldForever = new();
        RelayLink held = new(
            new HubConnector(
                relay.Address,
                TimeProvider.System,
                wait,
                connection => connection.On(replacedMethod, () => {
                    entered.TrySetResult();
                    return heldForever.Task;
                })),
            NextHandleAsync,
            TimeProvider.System,
            () => jitter,
            logger);
        running = held.RunAsync(stop.Token);
        await using BrowserHub browser = await BrowserHub.ConnectAsync(relay.Address, await BrowserHandleAsync());
        await browser.WaitForCliAsync();
        HubConnector other = new(relay.Address, TimeProvider.System);

        await using IRelayConnection replacing =
            await other.ConnectAsync(await api.CliHandleAsync(keys.Signing, Token), Token);

        await running.WaitAsync(wait, Token);
        await entered.Task.WaitAsync(wait, Token);
        Assert.Single(handles);
    }

    [Fact]
    public async Task ShouldEndRun_WhenStopIsCancelled() {
        running = link.RunAsync(stop.Token);
        await using BrowserHub browser = await BrowserHub.ConnectAsync(relay.Address, await BrowserHandleAsync());
        await browser.WaitForCliAsync();

        await stop.CancelAsync();

        await running.WaitAsync(wait, Token);
    }

    [Fact]
    public async Task ShouldDeliverFrame_WhenLinkIsConnected() {
        running = link.RunAsync(stop.Token);
        await using BrowserHub browser = await BrowserHub.ConnectAsync(relay.Address, await BrowserHandleAsync());
        await browser.WaitForCliAsync();
        await RelayLinkLogs.ConnectedAsync(logger);

        await link.SendAsync(EnvelopeFixtures.Hello(keys.Channel.Value, EnvelopeFixtures.Sid), Token);

        Envelope delivered = await browser.NextReceivedAsync();
        Assert.Equal(EnvelopeFixtures.Sid, delivered.Sid);
    }

    [Fact]
    public async Task ShouldRefuseSend_WhenRelayStopped() {
        running = link.RunAsync(stop.Token);
        await RelayLinkLogs.ConnectedAsync(logger);
        await relay.StopAsync();
        await NextAsync<RelayEvent.Disconnected>();

        RelayUnavailableException refused = await Assert.ThrowsAsync<RelayUnavailableException>(
            () => link.SendAsync(EnvelopeFixtures.Hello(keys.Channel.Value, EnvelopeFixtures.Sid), Token));

        Assert.Equal(notConnected, refused.Message);
    }

    [Fact]
    public async Task ShouldRefuseSend_WhenLinkIsNotConnected() {
        Envelope envelope = EnvelopeFixtures.Hello(keys.Channel.Value, EnvelopeFixtures.Sid);

        await Assert.ThrowsAsync<RelayUnavailableException>(() => link.SendAsync(envelope, Token));
    }

    private async Task<string> NextHandleAsync(CancellationToken cancellationToken) {
        string handle = await api.CliHandleAsync(keys.Signing, cancellationToken);
        handles.Enqueue(handle);
        return handle;
    }

    private async Task<string> BrowserHandleAsync() {
        PairingStart start = await api.StartPairingAsync(keys.Signing, Token);
        return await BrowserHub.ClaimAsync(relay.Address, start.Code);
    }

    private async Task ReconnectAsync() {
        running = link.RunAsync(stop.Token);
        string browserHandle = await BrowserHandleAsync();
        await using (BrowserHub before = await BrowserHub.ConnectAsync(relay.Address, browserHandle)) {
            await before.WaitForCliAsync();
        }
        await relay.RestartAsync();
        await NextAsync<RelayEvent.Disconnected>();
        await using BrowserHub after = await BrowserHub.ConnectAsync(relay.Address, browserHandle);
        await after.WaitForCliAsync();
    }

    private async Task<T> NextAsync<T>() where T : RelayEvent {
        while (true) {
            RelayEvent next = await link.ReceiveAsync(Token).AsTask().WaitAsync(wait, Token);
            if (next is T wanted) {
                return wanted;
            }
        }
    }
}
