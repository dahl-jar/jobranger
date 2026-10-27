using System.Net;
using System.Text.Json.Nodes;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Jobranger.Relay.Tests;

public sealed class NonceLimitTests(RelayFactory factory) : IClassFixture<RelayFactory> {
    private const string nonceRoute = "/pair/nonce";
    private const int noncesPerAddress = 30;
    private const int requestsPerChannel = 30;
    private const int floodClients = 100;
    private const int pairedCliReconnects = 10;
    private const int noncesPerNetwork = 64;
    private const int noncesPerChannel = 4;
    private const int secondOctet = 65;
    private const int failedSessionsPerAddress = 120;
    private const int clientsPerBlockShare = 5;

    private static readonly TimeSpan elapsedInWindow = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan leftInWindow = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan nonceWindow = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task ShouldLetPairedCliReconnectDuringAddressFlood() {
        using WebApplicationFactory<Program> capped = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(new RelayRateLimiter(factory.Time, maxPartitions: 1, overflowShards: 1))));
        using CliKey key = new();
        using RelayApi owner = RelayApi.For(capped, "192.0.2.20");
        JsonObject paired = await owner.Paired(key);
        string channel = (string)paired["channel"]!;
        for (int client = 1; client <= floodClients; client++) {
            using RelayApi flood = RelayApi.For(capped, $"198.18.0.{client}");
            using HttpResponseMessage response = await flood.Post(nonceRoute, RelayApi.NonceBody(null));
        }

        using RelayApi reconnecting = RelayApi.For(capped, "198.18.0.200");
        int refused = 0;
        for (int reconnect = 0; reconnect < pairedCliReconnects; reconnect++) {
            using HttpResponseMessage nonce = await reconnecting.Post(nonceRoute, RelayApi.NonceBody(channel));
            JsonObject issued = await RelayApi.Json(nonce);
            using HttpResponseMessage session = await reconnecting.Post("/session/cli", key.Proof(CliKey.SessionCli, (string)issued["nonce"]!));
            refused += nonce.StatusCode == HttpStatusCode.OK && session.StatusCode == HttpStatusCode.OK ? 0 : 1;
        }

        Assert.Equal(0, refused);
    }

    [Fact]
    public async Task ShouldChargeUnpairedChannelToAddressTier() {
        using CliKey key = new();
        using RelayApi starter = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        JsonObject started = await starter.StartPairing(key);
        string channel = (string)started["channel"]!;
        using RelayApi api = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        for (int request = 0; request < noncesPerAddress; request++) {
            using HttpResponseMessage admitted = await api.Post(nonceRoute, RelayApi.NonceBody(null));
        }

        using HttpResponseMessage refused = await api.Post(nonceRoute, RelayApi.NonceBody(channel));

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Fact]
    public async Task ShouldRefusePairedChannelPastItsLimit() {
        using CliKey key = new();
        using RelayApi owner = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        JsonObject paired = await owner.Paired(key);
        string channel = (string)paired["channel"]!;
        using (await FromNewAddress(RelayApi.NonceBody(channel))) {
            factory.Time.Advance(elapsedInWindow);
        }

        for (int request = 1; request < requestsPerChannel; request++) {
            using HttpResponseMessage admitted = await FromNewAddress(RelayApi.NonceBody(channel));
        }

        using HttpResponseMessage refused = await FromNewAddress(RelayApi.NonceBody(channel));

        Assert.Equal((HttpStatusCode.TooManyRequests, leftInWindow), (refused.StatusCode, refused.Headers.RetryAfter?.Delta));
    }

    [Fact]
    public async Task ShouldIssueChannelNoncesWhenNetworkIsFull() {
        using CliKey key = new();
        using RelayApi owner = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        JsonObject paired = await owner.Paired(key);
        string channel = (string)paired["channel"]!;
        for (int issued = 0; issued < noncesPerNetwork; issued++) {
            using RelayApi neighbour = RelayApi.For(factory, $"100.66.5.{(issued / noncesPerAddress) + 1}");
            using HttpResponseMessage response = await neighbour.Post(nonceRoute, RelayApi.NonceBody(null));
        }

        using RelayApi reconnecting = RelayApi.For(factory, "100.66.5.200");
        List<HttpStatusCode> statuses = [];
        for (int request = 0; request <= noncesPerChannel; request++) {
            using HttpResponseMessage response = await reconnecting.Post(nonceRoute, RelayApi.NonceBody(channel));
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            [.. Enumerable.Repeat(HttpStatusCode.OK, noncesPerChannel), HttpStatusCode.ServiceUnavailable],
            statuses);
    }

    [Fact]
    public async Task ShouldReconnectCliWhileIpv4NeighboursHoldNetworkNonces() {
        using CliKey key = new();
        for (int issued = 0; issued < noncesPerNetwork; issued++) {
            using RelayApi neighbour = RelayApi.For(factory, $"100.67.5.{(issued / noncesPerAddress) + 1}");
            using HttpResponseMessage response = await neighbour.Post(nonceRoute, RelayApi.NonceBody(null));
        }

        (HttpStatusCode, HttpStatusCode, HubConnectionState) reconnect = await Reconnect(key, "100.67.5.200");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HubConnectionState.Connected), reconnect);
    }

    [Fact]
    public async Task ShouldReconnectCliWhileIpv6NeighboursUseUpBlockLimits() {
        using CliKey key = new();
        string block = Addresses.NextBlock();
        for (int prefix = 1; prefix <= clientsPerBlockShare; prefix++) {
            using RelayApi neighbour = RelayApi.For(factory, $"{block}:{prefix:x}::1");
            for (int attempt = 0; attempt < failedSessionsPerAddress; attempt++) {
                using HttpResponseMessage failed = await neighbour.WebSession("not-a-ticket");
            }

            for (int request = 0; request < noncesPerAddress; request++) {
                using HttpResponseMessage nonce = await neighbour.Post(nonceRoute, RelayApi.NonceBody(null));
            }
        }

        (HttpStatusCode, HttpStatusCode, HubConnectionState) reconnect = await Reconnect(key, $"{block}:ffff::1");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HubConnectionState.Connected), reconnect);
    }

    [Fact]
    public async Task ShouldChargeCliNoncesToChannelAfterSessionProof() {
        using CliKey key = new();
        using RelayApi api = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        using (await api.Post("/session/cli", key.Proof(CliKey.SessionCli, await api.Nonce()))) {
            for (int request = 1; request < noncesPerAddress; request++) {
                using HttpResponseMessage admitted = await api.Post(nonceRoute, RelayApi.NonceBody(null));
            }
        }

        using HttpResponseMessage nonce = await api.Post(nonceRoute, RelayApi.NonceBody(key.Channel.Value));

        Assert.Equal(HttpStatusCode.OK, nonce.StatusCode);
    }

    [Fact]
    public async Task ShouldRefuseUnknownChannelPastItsNonceLimit() {
        using CliKey key = new();
        for (int request = 0; request < noncesPerAddress; request++) {
            using HttpResponseMessage admitted = await FromNewAddress(RelayApi.NonceBody(key.Channel.Value));
        }

        using HttpResponseMessage refused = await FromNewAddress(RelayApi.NonceBody(key.Channel.Value));

        Assert.Equal((HttpStatusCode.TooManyRequests, nonceWindow), (refused.StatusCode, refused.Headers.RetryAfter?.Delta));
    }

    private async Task<(HttpStatusCode Nonce, HttpStatusCode Session, HubConnectionState Hub)> Reconnect(CliKey key, string address) {
        using RelayApi api = RelayApi.For(factory, address);
        using HttpResponseMessage nonce = await api.Post(nonceRoute, RelayApi.NonceBody(key.Channel.Value));
        if (nonce.StatusCode != HttpStatusCode.OK) {
            return (nonce.StatusCode, default, HubConnectionState.Disconnected);
        }

        using HttpResponseMessage session = await api.Post("/session/cli", key.Proof(CliKey.SessionCli, (string)(await RelayApi.Json(nonce))["nonce"]!));
        if (session.StatusCode != HttpStatusCode.OK) {
            return (nonce.StatusCode, session.StatusCode, HubConnectionState.Disconnected);
        }

        await using HubPeer cli = await RelayClients.ConnectAsync(factory, (string)(await RelayApi.Json(session))["handle"]!, null, address);
        return (nonce.StatusCode, session.StatusCode, cli.Connection.State);
    }

    private async Task<HttpResponseMessage> FromNewAddress(JsonObject body) {
        using RelayApi api = RelayApi.For(factory, Addresses.NextIpv4(secondOctet));
        return await api.Post(nonceRoute, body);
    }
}
