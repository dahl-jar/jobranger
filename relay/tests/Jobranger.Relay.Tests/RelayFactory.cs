using System.Security.Cryptography;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Hubs;
using Jobranger.Relay.Tests.Mocks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Tests;

public sealed class RelayFactory : WebApplicationFactory<Program> {
    public const string SiteOrigin = "https://site.test";
    public const string ProxyNetwork = "10.42.0.0/16";
    public const string DefaultLogKey = "Logging:LogLevel:Default";

    public static readonly string HandleKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public static readonly string RedactionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));

    public FakeLogCollector Logs => Services.GetFakeLogCollector();

    public int? HubSocketCap { get; set; }

    public string? DebugLogKey { get; set; }

    internal HubRequestEnds HubRequestEnds { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseSetting("Relay:SiteOrigin", SiteOrigin);
        builder.UseSetting("Relay:HandleKey", HandleKey);
        builder.UseSetting("Relay:RedactionKey", RedactionKey);
        builder.UseSetting("Relay:KnownProxyNetworks:0", ProxyNetwork);
        builder.ConfigureLogging(logging => logging.AddFakeLogging());
        if (DebugLogKey is { } debugKey) {
            builder.ConfigureAppConfiguration(DropAppSettings);
            builder.UseSetting(DefaultLogKey, nameof(LogLevel.Debug));
            builder.UseSetting(debugKey, nameof(LogLevel.Debug));
        }
        builder.ConfigureTestServices(services => {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IStartupFilter>(HubRequestEnds);
            services.AddSingleton<IStartupFilter, PeerAddressFilter>();
            services.AddSingleton<IStartupFilter, LocalPortFilter>();
            if (HubSocketCap is { } cap) {
                services.AddSingleton(new HubSocketGate(cap, RelayLimits.MaxHubSocketsPerNetwork));
            }
            services.AddSingleton<IStartupFilter, BodyLimitFilter>();
        });
    }

    private static void DropAppSettings(IConfigurationBuilder config) {
        foreach (JsonConfigurationSource json in config.Sources.OfType<JsonConfigurationSource>().ToList()) {
            config.Sources.Remove(json);
        }
    }
}
