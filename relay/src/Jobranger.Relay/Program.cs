using System.Text.Json.Serialization;
using Jobranger.Relay;
using Jobranger.Relay.Core;
using Jobranger.Relay.Options;
using Jobranger.Relay.Pairing;
using Jobranger.Relay.Security;
using Jobranger.Relay.Telemetry;
using Microsoft.Extensions.Options;

const long maxRequestBodyBytes = 16_384;
const int startupFailureExitCode = 1;
const string startupCategory = "Jobranger.Relay.Startup";

try
{
    WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

    builder.Logging.AddRelayJsonConsole();
    builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = maxRequestBodyBytes);
    builder.Services.AddOptions<RelayOptions>().BindConfiguration(RelayOptions.Section).ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<RelayOptions>, RelayOptionsValidator>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton(services => services.GetRequiredService<IOptions<RelayOptions>>().Value.ToHandleKeys());
    builder.Services.AddSingleton<ChannelHandle>();
    builder.Services.AddSingleton(services => new PairingStore(services.GetRequiredService<TimeProvider>()));
    builder.Services.AddSingleton(services => new NonceStore(services.GetRequiredService<TimeProvider>()));
    builder.Services.AddSingleton(services => new PairedChannels(services.GetRequiredService<TimeProvider>()));
    builder.Services.ConfigureHttpJsonOptions(json =>
    {
        json.SerializerOptions.TypeInfoResolverChain.Insert(0, RelayJsonContext.Default);
        json.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        json.SerializerOptions.AllowDuplicateProperties = false;
    });
    builder.Services.Configure<RouteHandlerOptions>(routes => routes.ThrowOnBadRequest = false);
    builder.Services.AddProblemDetails(problems => problems.CustomizeProblemDetails = RelayProblems.Customize);
    builder.Services.AddProxyHeaders();
    builder.Services.AddSiteCors();
    builder.Services.AddRelayRateLimits();
    builder.AddRelayRedaction();

    WebApplication app = builder.Build();

    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseCors();
    app.UseRateLimiter();
    app.Use(FailureCharge.ChargeBadRequests);
    app.MapPairing();
    app.MapSessions();

    app.Run();
    return 0;
}
catch (Exception failure) when (failure is not HostAbortedException)
{
    using ILoggerFactory startup = LoggerFactory.Create(logging => logging.AddRelayJsonConsole());
    startup.CreateLogger(startupCategory).StartupFailed(failure);
    return startupFailureExitCode;
}
