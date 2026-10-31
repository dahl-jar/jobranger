using System.Threading.Channels;
using Jobranger.Messaging.Envelopes;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Link;

internal sealed partial class RelayLink(
    IRelayConnector connector,
    Func<CancellationToken, Task<string>> nextHandle,
    TimeProvider time,
    Func<double> jitter,
    ILogger logger) {
    private readonly Channel<RelayEvent> events = Channel.CreateUnbounded<RelayEvent>();
    private readonly ReconnectBackoff backoff = new();
    private volatile IRelayConnection? current;

    public async Task RunAsync(CancellationToken stop) {
        try {
            while (true) {
                (bool replaced, TimeSpan connectedFor, TimeSpan retryAfter) =
                    await AttemptAsync(stop).ConfigureAwait(false);
                if (replaced) {
                    LogReplaced(logger);
                    return;
                }
                TimeSpan delay = backoff.Next(connectedFor, jitter(), retryAfter);
                LogRetrying(logger, (long)delay.TotalMilliseconds);
                await Task.Delay(delay, time, stop).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
        }
    }

    public Task SendAsync(Envelope envelope, CancellationToken cancellationToken) =>
        (current ?? throw new RelayUnavailableException("relay link is not connected")).SendAsync(envelope, cancellationToken);

    public ValueTask<RelayEvent> ReceiveAsync(CancellationToken cancellationToken) =>
        events.Reader.ReadAsync(cancellationToken);

    private async Task<(bool Replaced, TimeSpan ConnectedFor, TimeSpan RetryAfter)> AttemptAsync(CancellationToken stop) {
        IRelayConnection connection;
        try {
            string handle = await nextHandle(stop).ConfigureAwait(false);
            connection = await connector.ConnectAsync(handle, stop).ConfigureAwait(false);
        } catch (Exception error) when (error is RelayUnavailableException or RelayRejectedException) {
            LogFailed(logger, error.GetType().Name);
            TimeSpan retryAfter = error is RelayUnavailableException unavailable ? unavailable.RetryAfter : TimeSpan.Zero;
            return (false, TimeSpan.Zero, retryAfter);
        }
        DateTimeOffset connectedAt = time.GetUtcNow();
        current = connection;
        LogConnected(logger);
        try {
            bool replaced = await PumpAsync(connection, stop).ConfigureAwait(false);
            return (replaced, time.GetUtcNow() - connectedAt, TimeSpan.Zero);
        } finally {
            current = null;
            await connection.DisposeAsync().ConfigureAwait(false);
            events.Writer.TryWrite(new RelayEvent.Disconnected());
        }
    }

    private async Task<bool> PumpAsync(IRelayConnection connection, CancellationToken stop) {
        while (true) {
            RelayEvent next = await connection.ReceiveAsync(stop).ConfigureAwait(false);
            if (next is RelayEvent.Disconnected) {
                return false;
            }
            if (next is RelayEvent.Replaced) {
                return true;
            }
            events.Writer.TryWrite(next);
        }
    }

    [LoggerMessage(EventName = "link.relay.connected", Level = LogLevel.Information, Message = "connected to the relay")]
    private static partial void LogConnected(ILogger logger);

    [LoggerMessage(
        EventName = "link.relay.failed",
        Level = LogLevel.Warning,
        Message = "could not connect to the relay error={Error}")]
    private static partial void LogFailed(ILogger logger, string error);

    [LoggerMessage(
        EventName = "link.relay.retrying",
        Level = LogLevel.Information,
        Message = "reconnecting to the relay delay_ms={DelayMs}")]
    private static partial void LogRetrying(ILogger logger, long delayMs);

    [LoggerMessage(
        EventName = "link.relay.replaced",
        Level = LogLevel.Warning,
        Message = "stopped reconnecting, another jobranger replaced the relay link")]
    private static partial void LogReplaced(ILogger logger);
}
