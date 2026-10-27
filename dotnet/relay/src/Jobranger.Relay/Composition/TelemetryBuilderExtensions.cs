using Jobranger.Relay.Telemetry;

namespace Jobranger.Relay.Composition;

internal static class TelemetryBuilderExtensions {
    public static WebApplicationBuilder AddRelayTelemetry(this WebApplicationBuilder builder) {
        builder.Logging.AddRelayJsonConsole();
        builder.Logging.AddRequestLogFloor();
        builder.Services.AddRelayMetrics();
        builder.AddRelayRedaction();
        return builder;
    }
}
