using System.Net;
using System.Text.Json.Nodes;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Jobranger.Relay.Tests;

public sealed class SessionLimitTests(RelayFactory factory) : IClassFixture<RelayFactory> {
    private const string cliRoute = "/session/cli";
    private const string webRoute = "/session/web";
    private const int sessionsPerChannel = 30;
    private const int failedSessionsPerAddress = 120;
    private const int floodClients = 300;
    private const int addressesPerNetwork = 250;
    private const int secondOctet = 64;

    private static readonly TimeSpan elapsedInWindow = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan leftInWindow = TimeSpan.FromMinutes(6);

    [Fact]
    public async Task ShouldIssueHandlesToPairedChannelDuringAddressFlood() {
        using WebApplicationFactory<Program> capped = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(new RelayRateLimiter(factory.Time, maxPartitions: 1, overflowShards: 1))));
        using CliKey key = new();
        using RelayApi owner = RelayApi.For(capped, "192.0.2.10");
        JsonObject paired = await owner.Paired(key);
        string ticket = (string)paired["ticket"]!;
        for (int client = 1; client <= floodClients; client++) {
            using RelayApi flood = RelayApi.For(capped, $"198.18.{client / addressesPerNetwork}.{(client % addressesPerNetwork) + 1}");
            using HttpResponseMessage refused = await flood.WebSession("not-a-ticket");
        }

        using RelayApi reconnecting = RelayApi.For(capped, "198.18.0.251");
        using HttpResponseMessage cli = await reconnecting.Post(cliRoute, key.Proof(CliKey.SessionCli, await reconnecting.Nonce()));
        using HttpResponseMessage web = await reconnecting.WebSession(ticket);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (cli.StatusCode, web.StatusCode));
    }

    [Theory]
    [InlineData(cliRoute)]
    [InlineData(webRoute)]
    public async Task ShouldRefuseChannelPastSessionLimit(string route) {
        using CliKey key = new();
        using RelayApi owner = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        JsonObject paired = await owner.Paired(key);
        string ticket = (string)paired["ticket"]!;
        using (await Session(route, key, ticket)) {
            factory.Time.Advance(elapsedInWindow);
        }

        for (int request = 1; request < sessionsPerChannel; request++) {
            using HttpResponseMessage admitted = await Session(route, key, ticket);
        }

        using HttpResponseMessage refused = await Session(route, key, ticket);

        Assert.Equal((HttpStatusCode.TooManyRequests, leftInWindow), (refused.StatusCode, refused.Headers.RetryAfter?.Delta));
    }

    [Fact]
    public async Task ShouldRefuseFailedSessionsPastAddressLimit() {
        using RelayApi api = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        for (int attempt = 1; attempt < failedSessionsPerAddress; attempt++) {
            using HttpResponseMessage failed = await api.WebSession("not-a-ticket");
        }

        using HttpResponseMessage last = await api.WebSession("not-a-ticket");
        using HttpResponseMessage refused = await api.WebSession("not-a-ticket");

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests), (last.StatusCode, refused.StatusCode));
    }

    private async Task<HttpResponseMessage> Session(string route, CliKey key, string ticket) {
        using RelayApi api = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        return route == cliRoute
            ? await api.Post(cliRoute, key.Proof(CliKey.SessionCli, await api.Nonce()))
            : await api.WebSession(ticket);
    }
}
