using Jobranger.Relay.Core;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Jobranger.Relay.Health;

internal static class HealthSetup {
    private const string livePath = "/healthz";
    private const string readyPath = "/readyz";
    private const string liveTag = "live";
    private const string readyTag = "ready";
    private const string sweeperLiveCheck = "sweeper-live";
    private const string sweeperReadyCheck = "sweeper-ready";
    private const string stoppingCheck = "stopping";
    private const string healthCheckCategory = "Microsoft.Extensions.Diagnostics.HealthChecks";

    public static IServiceCollection AddRelayHealth(this IServiceCollection services) {
        services.AddSingleton<SweeperHeartbeat>();
        services.AddSingleton<ISweepable>(provider => provider.GetRequiredService<PairingStore>());
        services.AddSingleton<ISweepable>(provider => provider.GetRequiredService<NonceStore>());
        services.AddSingleton<ISweepable>(provider => provider.GetRequiredService<PairedChannels>());
        services.AddSingleton<ISweepable>(provider => provider.GetRequiredService<RelayRateLimiter>());
        services.AddHostedService<SweeperService>();
        services.AddLogging(logging => logging.AddFilter(healthCheckCategory, LogLevel.None));
        services.AddHealthChecks()
            .Add(SweeperCheck(sweeperLiveCheck, RelayLimits.SweeperDeadAfter, liveTag))
            .Add(SweeperCheck(sweeperReadyCheck, RelayLimits.SweeperStaleAfter, readyTag))
            .AddCheck<StoppingHealthCheck>(stoppingCheck, tags: [readyTag]);
        return services;
    }

    public static WebApplication MapRelayHealth(this WebApplication app) {
        app.MapHealthChecks(livePath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(liveTag) });
        app.MapHealthChecks(readyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(readyTag) });
        return app;
    }

    private static HealthCheckRegistration SweeperCheck(string name, TimeSpan limit, string tag) => new(
        name,
        provider => new SweeperHealthCheck(
            provider.GetRequiredService<SweeperHeartbeat>(), provider.GetRequiredService<TimeProvider>(), limit),
        failureStatus: null,
        tags: [tag]);
}
