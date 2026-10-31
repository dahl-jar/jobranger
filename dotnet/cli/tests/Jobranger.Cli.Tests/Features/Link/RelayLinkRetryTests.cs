using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Cli.Tests.Features.Link;

public sealed class RelayLinkRetryTests : IAsyncDisposable {
    private const string handle = "cli-handle";
    private const string connectedEvent = "link.relay.connected";

    private static readonly TimeSpan connectedLong = TimeSpan.FromSeconds(60);

    private readonly RecordingTime time = new();
    private readonly FakeLogger logger = new();
    private readonly FakeRelay relay;
    private readonly CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
    private Task running = Task.CompletedTask;

    public RelayLinkRetryTests() {
        relay = new FakeRelay(time) { ConnectFailure = new RelayUnavailableException("hub down") };
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync() {
        await stop.CancelAsync();
        try {
            await running;
        } finally {
            relay.Dispose();
            stop.Dispose();
        }
    }

    [Fact]
    public async Task ShouldWaitBackoff_WhenConnectFails() {
        Run(relay);
        TimeSpan first = await time.NextWaitAsync();
        int attemptsBeforeWait = relay.Handles.Count;

        time.Advance(first);

        await TestWaits.UntilAsync(() => relay.Handles.Count == 2);
        Assert.Equal((TimeSpan.FromSeconds(1), 1), (first, attemptsBeforeWait));
    }

    [Fact]
    public async Task ShouldStopReconnecting_WhenReplacedArrivesWithoutClose() {
        relay.ConnectFailure = null;
        relay.Replace();

        Run(relay);

        await running.WaitAsync(TestWaits.Step, Token);
        Assert.Single(relay.Handles);
    }

    [Fact]
    public async Task ShouldResetBackoff_WhenLinkWasConnectedForSixtySeconds() {
        Run(relay);
        time.Advance(await time.NextWaitAsync());
        TimeSpan grown = await time.NextWaitAsync();
        relay.ConnectFailure = null;
        time.Advance(grown);
        await TestWaits.UntilAsync(() => logger.Collector.GetSnapshot().Any(record => record.Id.Name == connectedEvent));
        time.Advance(connectedLong);

        relay.Disconnect();

        Assert.Equal(TimeSpan.FromSeconds(1), await time.NextWaitAsync());
    }

    [Theory]
    [InlineData(30, 30)]
    [InlineData(900, 300)]
    public async Task ShouldWaitRetryAfter_WhenRelayIsFull(int retryAfterSeconds, int waitSeconds) {
        await using FakeHub hub = FakeHub.Busy(retryAfterSeconds);

        Run(new HubConnector(hub.Address, TimeProvider.System));

        Assert.Equal(TimeSpan.FromSeconds(waitSeconds), await time.NextWaitAsync());
    }

    private void Run(IRelayConnector connector) {
        RelayLink link = new(connector, NextHandleAsync, time, () => 1.0, logger);
        running = Task.Run(() => link.RunAsync(stop.Token), Token);
    }

    private static Task<string> NextHandleAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(handle);
    }
}
