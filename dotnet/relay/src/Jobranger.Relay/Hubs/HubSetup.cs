using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Jobranger.Relay.Hubs;

internal static class HubSetup {
    public const string Policy = "relay-handle";

    public static Task HideNegotiate(HttpContext http, RequestDelegate next) {
        if (http.GetEndpoint()?.Metadata.GetMetadata<NegotiateMetadata>() is null) {
            return next(http);
        }

        http.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    public static WebApplication MapRelayHub(this WebApplication app) {
        HubIdleWatch idle = app.Services.GetRequiredService<HubIdleWatch>();
        HttpConnectionDispatcherOptions options = new() {
            Transports = HttpTransportType.WebSockets,
            ApplicationMaxBufferSize = RelayLimits.HubApplicationBufferBytes,
            TransportMaxBufferSize = RelayLimits.HubTransportBufferBytes,
        };
        app.MapConnections(HandleAuthHandler.HubPath, options, connection => {
            connection.Use(idle.Wrap);
            connection.UseHub<RelayHub>();
        })
            .WithMetadata(new HubMetadata(typeof(RelayHub)))
            .RequireAuthorization(Policy)
            .RequireRelayLimit(RateLimiting.HubSockets)
            .RequireFailureLimit(RateLimiting.Session);
        return app;
    }
}
