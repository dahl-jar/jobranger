using Jobranger.Relay.Hubs;
using Jobranger.Relay.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Tests;

public sealed class HubDeliveryTests {
    private const string method = "Receive";

    private static readonly TimeSpan sendTimeout = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider time = new();
    private readonly HubConnections connections = new();
    private readonly FakeHubConnection stalled = new("web-stalled", stalls: true);
    private readonly FakeHubConnection reading = new("web-reading");
    private readonly FakeHubConnection otherReading = new("web-other");
    private readonly HubDelivery delivery;

    public HubDeliveryTests() {
        connections.Add(stalled);
        connections.Add(reading);
        connections.Add(otherReading);
        delivery = new HubDelivery(connections, time, new RefusalSampler(time), NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task ShouldSerializeMessageOnceForAllRecipients() {
        await delivery.Send([reading.ConnectionId, otherReading.ConnectionId], method, []);

        Assert.Same(Assert.Single(reading.Written), Assert.Single(otherReading.Written));
    }

    [Fact]
    public void ShouldDeliverToOtherConnectionsWhileOneStalls() {
        _ = delivery.Send([stalled.ConnectionId, reading.ConnectionId], method, []);

        Assert.Single(reading.Written);
        time.Advance(sendTimeout);
    }

    [Fact]
    public async Task ShouldCloseOnlyConnectionThatStallsPastSendTimeout() {
        Task sending = delivery.Send([stalled.ConnectionId, reading.ConnectionId], method, []);
        time.Advance(sendTimeout);

        await sending;

        Assert.Equal((true, false), (stalled.Aborted, reading.Aborted));
    }

    [Fact]
    public void ShouldCancelStalledSendAtSendTimeout() {
        _ = delivery.Send([stalled.ConnectionId], method, []);
        CancellationToken send = Assert.Single(stalled.StalledWrites);
        time.Advance(sendTimeout - TimeSpan.FromMilliseconds(1));
        bool early = send.IsCancellationRequested;

        time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Equal((false, true), (early, send.IsCancellationRequested));
    }

    [Fact]
    public async Task ShouldSkipConnectionThatAlreadyClosed() {
        connections.Remove(otherReading.ConnectionId);

        await delivery.Send([reading.ConnectionId, otherReading.ConnectionId], method, []);

        Assert.Equal((1, 0), (reading.Written.Count, otherReading.Written.Count));
    }
}
