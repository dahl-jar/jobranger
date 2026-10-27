using Jobranger.Relay.Core;
using Jobranger.Relay.Security;
using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Jobranger.Relay.Hubs;

internal sealed class HubDelivery(HubConnections connections, TimeProvider time, RefusalSampler sampler, ILoggerFactory loggers) {
    private const string samplerKey = "hub:send-timeout";

    public Task Send(IReadOnlyList<string> connectionIds, string method, object?[] args) {
        SerializedHubMessage message = new(new InvocationMessage(method, args));
        return Task.WhenAll(connectionIds
            .Select(connections.Find)
            .OfType<HubConnectionContext>()
            .Select(connection => SendOne(connection, message, method)));
    }

    private async Task SendOne(HubConnectionContext connection, SerializedHubMessage message, string method) {
        using CancellationTokenSource timeout = new(RelayLimits.HubSendTimeout, time);
        try {
            await connection.WriteAsync(message, timeout.Token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (timeout.IsCancellationRequested) {
            connection.Abort();
            if (sampler.ShouldLog(samplerKey)) {
                RelayLog.For(loggers).HubSendTimedOut(method);
            }
        }
    }
}
