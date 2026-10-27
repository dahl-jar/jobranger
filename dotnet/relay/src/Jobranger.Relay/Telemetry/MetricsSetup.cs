using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

namespace Jobranger.Relay.Telemetry;

internal static class MetricsSetup {
    public const int MetricsPort = 9464;

    private const string metricsPath = "/metrics";
    private const string kestrelMeter = "Microsoft.AspNetCore.Server.Kestrel";
    private const int scrapeCacheMilliseconds = 0;

    private static readonly string[] kestrelTags =
    [
        "network.transport",
        "network.type",
        "network.protocol.name",
        "network.protocol.version",
        "server.port",
        "tls.protocol.version",
        "error.type",
    ];

    private static readonly string[] meters =
    [
        RelayMetrics.MeterName,
        "Microsoft.AspNetCore.Hosting",
        kestrelMeter,
        "Microsoft.AspNetCore.Http.Connections",
        "Microsoft.AspNetCore.RateLimiting",
        "System.Runtime",
    ];

    public static IServiceCollection AddRelayMetrics(this IServiceCollection services) {
        services.AddSingleton<RelayMetrics>();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics
            .AddMeter(meters)
            .AddPrometheusExporter(prometheus => prometheus.ScrapeResponseCacheDurationMilliseconds = scrapeCacheMilliseconds));
        services.ConfigureOpenTelemetryMeterProvider((provider, metrics) => {
            IMeterFactory owner = provider.GetRequiredService<IMeterFactory>();
            metrics.AddView(instrument => Stream(instrument, owner));
        });
        return services;
    }

    public static IApplicationBuilder UseRelayMetrics(this IApplicationBuilder app) {
        app.ApplicationServices.GetRequiredService<RelayMetrics>();
        app.UseOpenTelemetryPrometheusScrapingEndpoint(IsScrape);
        return app.MapWhen(OnMetricsPort, branch => branch.Run(NotFound));
    }

    private static MetricStreamConfiguration? Stream(Instrument instrument, IMeterFactory owner) => instrument.Meter switch {
        { Scope: IMeterFactory scope } when !ReferenceEquals(scope, owner) => MetricStreamConfiguration.Drop,
        { Name: kestrelMeter } => new MetricStreamConfiguration { TagKeys = kestrelTags },
        _ => null,
    };

    private static bool IsScrape(HttpContext http) => OnMetricsPort(http) && http.Request.Path == metricsPath;

    private static bool OnMetricsPort(HttpContext http) => http.Connection.LocalPort == MetricsPort;

    private static Task NotFound(HttpContext http) {
        http.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}
