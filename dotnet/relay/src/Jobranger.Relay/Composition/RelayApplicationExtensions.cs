using Jobranger.Relay.Endpoints;
using Jobranger.Relay.Health;
using Jobranger.Relay.Hubs;
using Jobranger.Relay.Security;
using Jobranger.Relay.Telemetry;

namespace Jobranger.Relay.Composition;

internal static class RelayApplicationExtensions {
    public static WebApplication UseRelay(this WebApplication app) {
        app.UseRelayMetrics();
        app.Use(HubSetup.HideNegotiate);
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseCors();
        app.UseRateLimiter();
        app.Use(FailureCharge.ChargeBadRequests);
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(HubAdmission.Check);
        return app;
    }

    public static WebApplication MapRelay(this WebApplication app) {
        app.MapPairingEndpoints();
        app.MapSessionEndpoints();
        app.MapRelayHub();
        app.MapRelayHealth();
        return app;
    }
}
