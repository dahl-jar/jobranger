using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Cli.Tests.Features.Link;

public sealed class RelayLinkRetryTests : IAsyncDisposable {
    private const string replacedEvent = "link.relay.replaced";
    private const string retryingEvent = "link.relay.retrying";

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
        int retriesBeforeWait = logger.Collector.GetSnapshot().Count(record => record.Id.Name == retryingEvent);

        time.Advance(first);

        await TestWaits.UntilAsync(() => relay.Handles.Count == 2);
        Assert.Equal((TimeSpan.FromSeconds(1), 1, 1), (first, attemptsBeforeWait, retriesBeforeWait));
    }

    [Fact]
    public async Task ShouldStopReconnecting_WhenReplacedArrivesWithoutClose() {
        relay.ConnectFailure = null;
        relay.Replace();

        Run(relay);

        await running.WaitAsync(TestWaits.Step, Token);
        IReadOnlyList<FakeLogRecord> records = logger.Collector.GetSnapshot();
        Assert.Single(relay.Handles);
        Assert.Single(records, record => record.Id.Name == replacedEvent);
        Assert.DoesNotContain(records, record => record.Id.Name == retryingEvent);
    }

    [Fact]
    public async Task ShouldResetBackoff_WhenLinkWasConnectedForSixtySeconds() {
        Run(relay);
        time.Advance(await time.NextWaitAsync());
        TimeSpan grown = await time.NextWaitAsync();
        relay.ConnectFailure = null;
        time.Advance(grown);
        await RelayLinkLogs.ConnectedAsync(logger);
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
        return Task.FromResult(FakeRelay.CliHandle);
    }
}
