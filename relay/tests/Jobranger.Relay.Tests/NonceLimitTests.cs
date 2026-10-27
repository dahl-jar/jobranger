using System.Net;
using System.Text.Json.Nodes;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Jobranger.Relay.Tests;

public sealed class NonceLimitTests(RelayFactory factory) : IClassFixture<RelayFactory>
{
    private const string nonceRoute = "/pair/nonce";
    private const int noncesPerAddress = 30;
    private const int requestsPerChannel = 30;
    private const int floodClients = 100;
    private const int pairedCliReconnects = 10;
    private const int noncesPerNetwork = 64;
    private const int noncesPerChannel = 4;
    private const int secondOctet = 65;

    private static readonly TimeSpan elapsedInWindow = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan leftInWindow = TimeSpan.FromMinutes(6);

    [Fact]
    public async Task ShouldLetPairedCliReconnectDuringAddressFlood()
    {
        using var capped = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(new RelayRateLimiter(factory.Time, maxPartitions: 1, overflowShards: 1))));
        using CliKey key = new();
        using RelayApi owner = RelayApi.For(capped, "192.0.2.20");
        JsonObject paired = await owner.Paired(key);
        string channel = (string)paired["channel"]!;
        for (int client = 1; client <= floodClients; client++)
        {
            using RelayApi flood = RelayApi.For(capped, $"198.18.0.{client}");
            using HttpResponseMessage response = await flood.Post(nonceRoute, RelayApi.NonceBody(null));
        }

        using RelayApi reconnecting = RelayApi.For(capped, "198.18.0.200");
        int refused = 0;
        for (int reconnect = 0; reconnect < pairedCliReconnects; reconnect++)
        {
            using HttpResponseMessage nonce = await reconnecting.Post(nonceRoute, RelayApi.NonceBody(channel));
            JsonObject issued = await RelayApi.Json(nonce);
            using HttpResponseMessage session = await reconnecting.Post("/session/cli", key.Proof(CliKey.SessionCli, (string)issued["nonce"]!));
            refused += nonce.StatusCode == HttpStatusCode.OK && session.StatusCode == HttpStatusCode.OK ? 0 : 1;
        }

        Assert.Equal(0, refused);
    }

    [Fact]
    public async Task ShouldChargeUnpairedChannelToAddressTier()
    {
        using CliKey key = new();
        using RelayApi starter = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        JsonObject started = await starter.StartPairing(key);
        string channel = (string)started["channel"]!;
        using RelayApi api = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        for (int request = 0; request < noncesPerAddress; request++)
        {
            using HttpResponseMessage admitted = await api.Post(nonceRoute, RelayApi.NonceBody(null));
        }

        using HttpResponseMessage refused = await api.Post(nonceRoute, RelayApi.NonceBody(channel));

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Fact]
    public async Task ShouldRefusePairedChannelPastItsLimit()
    {
        using CliKey key = new();
        using RelayApi owner = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        JsonObject paired = await owner.Paired(key);
        string channel = (string)paired["channel"]!;
        using (await FromNewAddress(RelayApi.NonceBody(channel)))
        {
            factory.Time.Advance(elapsedInWindow);
        }

        for (int request = 1; request < requestsPerChannel; request++)
        {
            using HttpResponseMessage admitted = await FromNewAddress(RelayApi.NonceBody(channel));
        }

        using HttpResponseMessage refused = await FromNewAddress(RelayApi.NonceBody(channel));

        Assert.Equal((HttpStatusCode.TooManyRequests, leftInWindow), (refused.StatusCode, refused.Headers.RetryAfter?.Delta));
    }

    [Fact]
    public async Task ShouldIssueChannelNoncesWhenNetworkIsFull()
    {
        using CliKey key = new();
        using RelayApi owner = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        JsonObject paired = await owner.Paired(key);
        string channel = (string)paired["channel"]!;
        for (int issued = 0; issued < noncesPerNetwork; issued++)
        {
            using RelayApi neighbour = RelayApi.For(factory, $"100.66.5.{(issued / noncesPerAddress) + 1}");
            using HttpResponseMessage response = await neighbour.Post(nonceRoute, RelayApi.NonceBody(null));
        }

        using RelayApi reconnecting = RelayApi.For(factory, "100.66.5.200");
        List<HttpStatusCode> statuses = [];
        for (int request = 0; request <= noncesPerChannel; request++)
        {
            using HttpResponseMessage response = await reconnecting.Post(nonceRoute, RelayApi.NonceBody(channel));
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            [.. Enumerable.Repeat(HttpStatusCode.OK, noncesPerChannel), HttpStatusCode.ServiceUnavailable],
            statuses);
    }

    private async Task<HttpResponseMessage> FromNewAddress(JsonObject body)
    {
        using RelayApi api = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        return await api.Post(nonceRoute, body);
    }
}
