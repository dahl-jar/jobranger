using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.SignalR;

namespace Jobranger.Relay.Hubs;

internal sealed class HubErrorFilter(ILoggerFactory loggers) : IHubFilter {
    private const string connectedStep = "OnConnectedAsync";
    private const string disconnectedStep = "OnDisconnectedAsync";

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next) {
        try {
            return await next(invocationContext);
        } catch (Exception failure) when (Report(failure, invocationContext.HubMethodName)) {
            throw;
        }
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next) {
        try {
            await next(context);
        } catch (Exception failure) when (Report(failure, connectedStep)) {
            throw;
        }
    }

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next) {
        try {
            await next(context, exception);
        } catch (Exception failure) when (Report(failure, disconnectedStep)) {
            throw;
        }
    }

    private bool Report(Exception failure, string step) {
        if (failure is not HubException) {
            RelayLog.For(loggers).HubFailed(failure, step);
        }

        return false;
    }
}
