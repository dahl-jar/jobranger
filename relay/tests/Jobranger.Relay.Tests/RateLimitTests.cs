using System.Net;
using System.Text.Json.Nodes;
using Jobranger.Relay.Security;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Jobranger.Relay.Tests;

public sealed class RateLimitTests(RelayFactory factory) : IClassFixture<RelayFactory>
{
    private const int claimBurst = 5;
    private const int noncesPerWindow = 30;
    private const int pairStartsPerPrefix64 = 10;
    private const int prefixes64PerBlock = 5;

    [Fact]
    public async Task ShouldReturn429AfterClaimBurstFromOneAddress()
    {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        await api.Claims(claimBurst);

        using HttpResponseMessage response = await api.Claim("BBBB-BBBB");

        Assert.Equal((HttpStatusCode.TooManyRequests, "rate_limited"), await RelayApi.Problem(response));
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task ShouldReportTimeLeftUntilNextClaimToken()
    {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        await api.Claims(claimBurst);
        factory.Time.Advance(TimeSpan.FromSeconds(10));

        using HttpResponseMessage response = await api.Claim("BBBB-BBBB");

        Assert.Equal(TimeSpan.FromSeconds(20), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task ShouldReportTimeLeftInNonceWindow()
    {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        for (int nonce = 0; nonce < noncesPerWindow; nonce++)
        {
            await api.Nonce();
        }

        factory.Time.Advance(TimeSpan.FromSeconds(25));

        using HttpResponseMessage response = await api.Post("/pair/nonce", RelayApi.NonceBody(null));

        Assert.Equal(TimeSpan.FromSeconds(35), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task ShouldTypeRateLimitProblem()
    {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        await api.Claims(claimBurst);

        using HttpResponseMessage response = await api.Claim("BBBB-BBBB");

        JsonObject body = await RelayApi.Json(response);
        Assert.Equal("https://tools.ietf.org/html/rfc6585#section-4", (string?)body["type"]);
    }

    [Fact]
    public async Task ShouldExposeRetryAfterToSite()
    {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        using HttpRequestMessage request = new(HttpMethod.Post, "/pair/claim")
        {
            Content = new StringContent("{\"v\":1,\"code\":\"BBBB-BBBB\"}", System.Text.Encoding.UTF8, RelayApi.JsonType),
        };
        request.Headers.Add("Origin", RelayFactory.SiteOrigin);

        using HttpResponseMessage response = await api.Send(request);

        Assert.Contains("Retry-After", response.Headers.GetValues("Access-Control-Expose-Headers"));
    }

    [Fact]
    public async Task ShouldKeepClaimLimitPerClientAddress()
    {
        using RelayApi limited = RelayApi.For(factory, Addresses.Next());
        using RelayApi other = RelayApi.For(factory, Addresses.Next());
        await limited.Claims(claimBurst);

        using HttpResponseMessage response = await other.Claim("BBBB-BBBB");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShouldShareClaimLimitAcrossOneIpv6Prefix64()
    {
        string prefix = Addresses.NextBlock();
        using RelayApi first = RelayApi.For(factory, $"{prefix}:0:0:0:0:1");
        using RelayApi second = RelayApi.For(factory, $"{prefix}:0:aaaa:bbbb:cccc:2");
        await first.Claims(claimBurst);

        using HttpResponseMessage response = await second.Claim("BBBB-BBBB");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ShouldLimitPairStartsAcrossOneIpv6Prefix48()
    {
        string block = Addresses.NextBlock();
        for (int prefix = 1; prefix <= prefixes64PerBlock; prefix++)
        {
            using RelayApi api = RelayApi.For(factory, $"{block}:{prefix:x}::1");
            await PairStarts(api, pairStartsPerPrefix64);
        }

        using RelayApi fresh = RelayApi.For(factory, $"{block}:ffff::1");
        using HttpResponseMessage response = await fresh.Post("/pair/start", new JsonObject { ["v"] = 1 });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ShouldConfinePairStartFloodToOneShardPerIpv4Network()
    {
        using var capped = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(new RelayRateLimiter(factory.Time, maxPartitions: 1))));
        using RelayApi known = RelayApi.For(capped, "192.0.2.1");
        await PairStarts(known, 1);
        for (int client = 1; client <= pairStartsPerPrefix64; client++)
        {
            using RelayApi flood = RelayApi.For(capped, $"203.0.113.{client}");
            await PairStarts(flood, 1);
        }

        using RelayApi neighbour = RelayApi.For(capped, "203.0.113.200");
        using HttpResponseMessage response = await neighbour.Post("/pair/start", new JsonObject { ["v"] = 1 });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task ShouldIgnoreForwardedAddressFromUntrustedPeer(string peer)
    {
        using RelayApi limited = RelayApi.For(factory, Addresses.Next(), peer);
        using RelayApi spoofed = RelayApi.For(factory, Addresses.Next(), peer);
        await limited.Claims(claimBurst);

        using HttpResponseMessage response = await spoofed.Claim("BBBB-BBBB");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    private static async Task PairStarts(RelayApi api, int count)
    {
        for (int start = 0; start < count; start++)
        {
            using HttpResponseMessage response = await api.Post("/pair/start", new JsonObject { ["v"] = 1 });
        }
    }
}
