using System.Net;
using Jobranger.Relay.Core;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Relay.Tests;

public sealed class HealthTests
{
    private const string livePath = "/healthz";
    private const string readyPath = "/readyz";
    private const string healthCheckCategory = "Microsoft.Extensions.Diagnostics.HealthChecks";
    private const int unhealthyProbes = 50;
    private const int sampledLines = 1;

    private static readonly TimeSpan oneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task ShouldReportReadyAfterStart()
    {
        using RelayFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpStatusCode status = await Status(client, readyPath);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task ShouldReportNotReadyWhenSweeperStalls()
    {
        using RelayFactory factory = new();
        using StalledStore stalled = new();
        using HttpClient client = (await Stall(factory, stalled, SweeperTiming.StaleAfter)).CreateClient();

        HttpStatusCode status = await Status(client, readyPath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
    }

    [Fact]
    public async Task ShouldStayLiveWhenSweeperStalls()
    {
        using RelayFactory factory = new();
        using StalledStore stalled = new();
        using HttpClient client = (await Stall(factory, stalled, SweeperTiming.DeadAfter - oneSecond)).CreateClient();

        HttpStatusCode status = await Status(client, livePath);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task ShouldStayReadyJustBeforeStaleLimit()
    {
        using RelayFactory factory = new();
        using StalledStore stalled = new();
        using HttpClient client = (await Stall(factory, stalled, SweeperTiming.StaleAfter - oneSecond)).CreateClient();

        HttpStatusCode status = await Status(client, readyPath);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task ShouldFailLivenessWhenSweeperStalledPastDeadline()
    {
        using RelayFactory factory = new();
        using StalledStore stalled = new();
        using HttpClient client = (await Stall(factory, stalled, SweeperTiming.DeadAfter)).CreateClient();

        HttpStatusCode status = await Status(client, livePath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
    }

    [Fact]
    public async Task ShouldNotLogEachUnhealthyProbe()
    {
        using RelayFactory factory = new();
        using StalledStore stalled = new();
        WebApplicationFactory<Program> host = await Stall(factory, stalled, SweeperTiming.StaleAfter);
        using HttpClient client = host.CreateClient();
        for (int probe = 0; probe < unhealthyProbes; probe++)
        {
            await Status(client, readyPath);
        }

        int lines = host.Services.GetFakeLogCollector().GetSnapshot()
            .Count(record => record.Category?.StartsWith(healthCheckCategory, StringComparison.Ordinal) == true);

        Assert.InRange(lines, 0, sampledLines);
    }

    [Fact]
    public void ShouldReportNotReadyWhileStopping()
    {
        using RelayFactory factory = new();
        using HttpClient client = factory.CreateClient();
        IHostApplicationLifetime lifetime = factory.Services.GetRequiredService<IHostApplicationLifetime>();
        HttpStatusCode? whileStopping = null;
        using CancellationTokenRegistration probe =
            lifetime.ApplicationStopping.Register(() => whileStopping = Status(client, readyPath).GetAwaiter().GetResult());

        lifetime.StopApplication();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, whileStopping);
    }

    [Fact]
    public void ShouldRegisterEveryStoreForSweeping()
    {
        using RelayFactory factory = new();
        IServiceProvider services = factory.Services;
        HashSet<object> stores =
        [
            services.GetRequiredService<PairingStore>(),
            services.GetRequiredService<NonceStore>(),
            services.GetRequiredService<PairedChannels>(),
            services.GetRequiredService<RelayRateLimiter>(),
        ];

        HashSet<object> swept = [.. services.GetServices<ISweepable>()];

        Assert.Equal(stores, swept);
    }

    private static async Task<WebApplicationFactory<Program>> Stall(RelayFactory factory, StalledStore stalled, TimeSpan stalledFor)
    {
        WebApplicationFactory<Program> host = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<ISweepable>(stalled)));
        _ = host.Server;
        _ = Task.Run(() => factory.Time.Advance(SweeperTiming.Period), TestContext.Current.CancellationToken);
        await stalled.Entered;
        factory.Time.Advance(stalledFor - SweeperTiming.Period);
        return host;
    }

    private static async Task<HttpStatusCode> Status(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }
}
