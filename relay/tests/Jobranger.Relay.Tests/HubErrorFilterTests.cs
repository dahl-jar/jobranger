using Jobranger.Relay.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Relay.Tests;

public sealed class HubErrorFilterTests : IDisposable {
    private readonly FakeLoggerProvider provider = new();
    private readonly LoggerFactory loggers;
    private readonly HubErrorFilter filter;

    public HubErrorFilterTests() {
        loggers = new LoggerFactory([provider]);
        filter = new HubErrorFilter(loggers);
    }

    [Fact]
    public async Task ShouldLogUnexpectedHubFailure() {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => filter.InvokeMethodAsync(Invocation(), _ => throw new InvalidOperationException("broken hub")).AsTask());

        FakeLogRecord record = Assert.Single(provider.Collector.GetSnapshot());

        Assert.Equal(LogLevel.Error, record.Level);
    }

    [Fact]
    public async Task ShouldNotLogHubRejection() {
        await Assert.ThrowsAsync<HubException>(
            () => filter.InvokeMethodAsync(Invocation(), _ => throw new HubException("invalid_envelope")).AsTask());

        IReadOnlyList<FakeLogRecord> records = provider.Collector.GetSnapshot();

        Assert.Empty(records);
    }

    [Fact]
    public async Task ShouldLogUnexpectedFailureWhileConnecting() {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => filter.OnConnectedAsync(Lifetime(), _ => throw new InvalidOperationException("broken join")));

        FakeLogRecord record = Assert.Single(provider.Collector.GetSnapshot());

        Assert.Equal(LogLevel.Error, record.Level);
    }

    [Fact]
    public async Task ShouldNotLogRefusedJoin() {
        await Assert.ThrowsAsync<HubException>(() => filter.OnConnectedAsync(Lifetime(), _ => throw new HubException("ticket_full")));

        IReadOnlyList<FakeLogRecord> records = provider.Collector.GetSnapshot();

        Assert.Empty(records);
    }

    [Fact]
    public async Task ShouldLogUnexpectedFailureWhileDisconnecting() {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => filter.OnDisconnectedAsync(Lifetime(), null, (_, _) => throw new InvalidOperationException("broken leave")));

        FakeLogRecord record = Assert.Single(provider.Collector.GetSnapshot());

        Assert.Equal(LogLevel.Error, record.Level);
    }

    public void Dispose() {
        loggers.Dispose();
        provider.Dispose();
    }

    private static HubInvocationContext Invocation() => new(
        new FakeCaller("cli-one"), new ServiceCollection().BuildServiceProvider(), new ProbeHub(), typeof(ProbeHub).GetMethod(nameof(ProbeHub.Send))!, []);

    private static HubLifetimeContext Lifetime() => new(new FakeCaller("cli-one"), new ServiceCollection().BuildServiceProvider(), new ProbeHub());

    private sealed class ProbeHub : Hub {
        public static void Send() {
        }
    }
}
