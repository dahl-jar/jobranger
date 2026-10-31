using Jobranger.Relay.Composition;
using Jobranger.Relay.Telemetry;

const int startupFailureExitCode = 1;
const string startupCategory = "Jobranger.Relay.Startup";

try {
    WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

    builder.Services.AddRelayOptions();
    builder.Services.AddRelayCore();
    builder.Services.AddRelaySecurity();
    builder.AddRelayTelemetry();
    builder.AddRelayHub();

    WebApplication app = builder.Build();

    app.UseRelay();
    app.MapRelay();

    app.Run();
    return 0;
} catch (Exception failure) when (failure is not HostAbortedException) {
    using ILoggerFactory startup = LoggerFactory.Create(logging => logging.AddRelayJsonConsole());
    startup.CreateLogger(startupCategory).StartupFailed(failure);
    return startupFailureExitCode;
}
