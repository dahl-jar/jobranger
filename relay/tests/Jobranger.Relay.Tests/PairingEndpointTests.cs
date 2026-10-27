using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Jobranger.Relay.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Jobranger.Relay.Tests;

public sealed partial class PairingEndpointTests(RelayFactory factory) : IClassFixture<RelayFactory>, IDisposable
{
    private const string vectorSeed = "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60";
    private const string vectorChannel = "sVpCkQYZPnMOnyB34PLScKMrmg2wDUTIPjk5VO0U880";
    private const int grantChars = 22;

    private static readonly TimeSpan nonceLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan codeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan handleLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ticketLifetime = TimeSpan.FromDays(90);

    private readonly RelayApi api = RelayApi.For(factory, Addresses.Next());

    [Fact]
    public async Task ShouldIssueNonceValidForSixtySeconds()
    {
        using HttpResponseMessage response = await api.Post("/pair/nonce", RelayApi.NonceBody(null));

        JsonObject body = await RelayApi.Json(response);
        Assert.Equal(
            (HttpStatusCode.OK, 43, factory.Time.GetUtcNow() + nonceLifetime),
            (response.StatusCode, ((string)body["nonce"]!).Length, (DateTimeOffset)body["expiresAt"]!));
    }

    [Fact]
    public async Task ShouldReturnCodeAndCliHandleForValidSignature()
    {
        using CliKey key = new();
        DateTimeOffset now = factory.Time.GetUtcNow();

        using HttpResponseMessage response = await api.Post("/pair/start", key.Proof(CliKey.PairStart, await api.Nonce()));

        JsonObject body = await RelayApi.Json(response);
        ChannelId channel = new((string)body["channel"]!);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(CodePattern(), (string)body["code"]!);
        Assert.Equal(now + codeLifetime, (DateTimeOffset)body["expiresAt"]!);
        Assert.Equal(now + handleLifetime, (DateTimeOffset)body["handleExpiresAt"]!);
        Assert.Equal(new HandleClaims(channel, RelayRole.Cli), Handles().Verify((string)body["handle"]!));
    }

    [Fact]
    public async Task ShouldDeriveChannelFromCliPublicKey()
    {
        using CliKey vector = new(Convert.FromHexString(vectorSeed));

        using HttpResponseMessage response = await api.Post("/pair/start", vector.Proof(CliKey.PairStart, await api.Nonce()));

        JsonObject body = await RelayApi.Json(response);

        Assert.Equal(vectorChannel, (string)body["channel"]!);
    }

    [Fact]
    public async Task ShouldRejectPairStartWithReusedNonce()
    {
        using CliKey key = new();
        string nonce = await api.Nonce();
        using HttpResponseMessage first = await api.Post("/pair/start", key.Proof(CliKey.PairStart, nonce));

        using HttpResponseMessage second = await api.Post("/pair/start", key.Proof(CliKey.PairStart, nonce));

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_nonce"), await RelayApi.Problem(second));
    }

    [Fact]
    public async Task ShouldRejectPairStartSignedByAnotherKey()
    {
        using CliKey key = new();
        using CliKey other = new();
        string nonce = await api.Nonce();
        JsonObject proof = key.Proof(CliKey.PairStart, nonce);
        proof["signature"] = other.Sign(CliKey.PairStart, nonce);

        using HttpResponseMessage response = await api.Post("/pair/start", proof);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_signature"), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldReturn410ForExpiredNonce()
    {
        using CliKey key = new();
        string nonce = await api.Nonce();
        factory.Time.Advance(nonceLifetime);

        using HttpResponseMessage response = await api.Post("/pair/start", key.Proof(CliKey.PairStart, nonce));

        Assert.Equal((HttpStatusCode.Gone, "nonce_expired"), await RelayApi.Problem(response));
    }

    [Theory]
    [InlineData("publicKey", "11qYAYKxCrfVS_7TyWQHOg7hcvPapiMlrwIaaPcHUR")]
    [InlineData("signature", "qVDD-Mm74_gqK9b6YfRv4d9RfDKugkD_My-wCZ7TwgmwMkgOqEJ5P1hX1VhBufwboU9p2ClTfd92Ny5TAMV7B")]
    [InlineData("nonce", null)]
    public async Task ShouldRejectProofWithMalformedField(string field, string? value)
    {
        using CliKey key = new();
        JsonObject proof = key.Proof(CliKey.PairStart, await api.Nonce());
        proof[field] = value;

        using HttpResponseMessage response = await api.Post("/pair/start", proof);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_request"), await RelayApi.Problem(response));
    }

    [Theory]
    [InlineData("/pair/nonce", 2, "unsupported_version")]
    [InlineData("/pair/claim", null, "invalid_request")]
    [InlineData("/session/web", 2, "unsupported_version")]
    public async Task ShouldRejectRequestWithoutSupportedVersion(string path, int? version, string code)
    {
        JsonObject body = new() { ["v"] = version, ["code"] = "KQFB-MXTW", ["ticket"] = "t" };

        using HttpResponseMessage response = await api.Post(path, body);

        Assert.Equal((HttpStatusCode.BadRequest, code), await RelayApi.Problem(response));
    }

    [Theory]
    [InlineData("/pair/nonce", "{\"v\":\"1\"}")]
    [InlineData("/pair/claim", "{\"v\":1,\"code\":\"BBBB-BBBB\",\"code\":\"BBBB-BBBB\"}")]
    [InlineData("/pair/claim", "{\"v\":1,")]
    public async Task ShouldRejectLooseJson(string path, string body)
    {
        using HttpResponseMessage response = await api.PostRaw(path, body);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_request"), await RelayApi.Problem(response));
    }

    [Theory]
    [InlineData("POST", "/pair/claim", "text/plain", HttpStatusCode.UnsupportedMediaType, "unsupported_media_type")]
    [InlineData("GET", "/pair/claim", null, HttpStatusCode.MethodNotAllowed, "method_not_allowed")]
    [InlineData("POST", "/pair/unknown", RelayApi.JsonType, HttpStatusCode.NotFound, "not_found")]
    public async Task ShouldNameFrameworkErrorInProblemCode(
        string method, string path, string? contentType, HttpStatusCode status, string code)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path);
        if (contentType is not null)
        {
            request.Content = new StringContent("{\"v\":1}", System.Text.Encoding.UTF8, contentType);
        }

        using HttpResponseMessage response = await api.Send(request);

        Assert.Equal((status, code), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldReturnWebHandleAndTicketForClaimedCode()
    {
        using CliKey key = new();
        JsonObject started = await api.StartPairing(key);
        DateTimeOffset now = factory.Time.GetUtcNow();

        using HttpResponseMessage response = await api.Claim((string)started["code"]!);

        JsonObject body = await RelayApi.Json(response);
        ChannelId channel = new((string)started["channel"]!);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(started["channel"]!.ToString(), (string)body["channel"]!);
        TicketCheck ticket = Handles().VerifyTicket((string)body["ticket"]!);
        Assert.Equal(new HandleClaims(channel, RelayRole.Web, ticket.Grant), Handles().Verify((string)body["handle"]!));
        Assert.Equal(now + handleLifetime, (DateTimeOffset)body["handleExpiresAt"]!);
        Assert.Equal((TicketOutcome.Valid, channel, grantChars), (ticket.Outcome, ticket.Channel, ticket.Grant?.Length));
        Assert.Equal(now + ticketLifetime, (DateTimeOffset)body["ticketExpiresAt"]!);
    }

    [Fact]
    public async Task ShouldClaimCodeTypedInLowercaseWithoutHyphen()
    {
        using CliKey key = new();
        JsonObject started = await api.StartPairing(key);
        string code = (string)started["code"]!;

        using HttpResponseMessage response = await api.Claim(code.Replace("-", " ", StringComparison.Ordinal).ToLowerInvariant());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ShouldReturn404ForWrongCode()
    {
        using CliKey key = new();
        JsonObject started = await api.StartPairing(key);
        string code = (string)started["code"]!;

        using HttpResponseMessage response = await api.Claim(Codes.WithWrongVerifier(code));

        Assert.Equal((HttpStatusCode.NotFound, "wrong_code"), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldAnswerUnknownSelectorAndWrongVerifierWithSame404Body()
    {
        using CliKey key = new();
        JsonObject started = await api.StartPairing(key);
        string code = (string)started["code"]!;
        using HttpResponseMessage wrongVerifier = await api.Claim(Codes.WithWrongVerifier(code));
        string wrongVerifierBody = await wrongVerifier.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage unknownSelector = await api.Claim(Codes.WithUnknownSelector(code));

        Assert.Equal(
            (HttpStatusCode.NotFound, wrongVerifierBody),
            (unknownSelector.StatusCode, await unknownSelector.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ShouldReturn410ForExpiredCode()
    {
        using CliKey key = new();
        JsonObject started = await api.StartPairing(key);
        string code = (string)started["code"]!;
        factory.Time.Advance(codeLifetime);

        using HttpResponseMessage response = await api.Claim(code);

        Assert.Equal((HttpStatusCode.Gone, "code_expired"), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldReturn400ForMalformedCode()
    {
        using HttpResponseMessage response = await api.Claim("K7QF-2M9X");

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_code_format"), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldReturn503WhenPendingLimitIsReached()
    {
        using var full = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(new PairingStore(factory.Time, maxPending: 0))));
        using RelayApi fullApi = RelayApi.For(full, Addresses.Next());
        using CliKey key = new();

        using HttpResponseMessage response = await fullApi.Post("/pair/start", key.Proof(CliKey.PairStart, await fullApi.Nonce()));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "relay_full"), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldReturn503ToNetworkAtPendingLimit()
    {
        using var shared = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(new PairingStore(factory.Time, maxPendingPerNetwork: 1))));
        using RelayApi first = RelayApi.For(shared, "100.101.1.1");
        using RelayApi sameNetwork = RelayApi.For(shared, "100.101.1.2");
        using RelayApi otherNetwork = RelayApi.For(shared, "100.101.2.1");
        using CliKey firstKey = new();
        using CliKey sameNetworkKey = new();
        using CliKey otherNetworkKey = new();
        await first.StartPairing(firstKey);

        using HttpResponseMessage refused = await sameNetwork.Post("/pair/start", sameNetworkKey.Proof(CliKey.PairStart, await sameNetwork.Nonce()));
        using HttpResponseMessage started = await otherNetwork.Post("/pair/start", otherNetworkKey.Proof(CliKey.PairStart, await otherNetwork.Nonce()));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "relay_full"), await RelayApi.Problem(refused));
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
    }

    [Fact]
    public async Task ShouldReturn503ToNetworkAtNonceLimit()
    {
        const int noncesPerNetwork = 64;
        const int noncesPerAddress = 30;
        for (int issued = 0; issued < noncesPerNetwork; issued++)
        {
            using RelayApi flood = RelayApi.For(factory, $"100.100.1.{(issued / noncesPerAddress) + 1}");
            await flood.Nonce();
        }

        using RelayApi sameNetwork = RelayApi.For(factory, "100.100.1.200");
        using RelayApi otherNetwork = RelayApi.For(factory, "100.100.2.1");
        using HttpResponseMessage refused = await sameNetwork.Post("/pair/nonce", RelayApi.NonceBody(null));
        using HttpResponseMessage issuedElsewhere = await otherNetwork.Post("/pair/nonce", RelayApi.NonceBody(null));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "relay_full"), await RelayApi.Problem(refused));
        Assert.Equal(HttpStatusCode.OK, issuedElsewhere.StatusCode);
    }

    [Fact]
    public async Task ShouldAllowCorsPreflightFromSiteOrigin()
    {
        using HttpRequestMessage preflight = new(HttpMethod.Options, "/pair/claim");
        preflight.Headers.Add("Origin", RelayFactory.SiteOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        using HttpResponseMessage response = await api.Send(preflight);

        Assert.Equal(
            (HttpStatusCode.NoContent, RelayFactory.SiteOrigin),
            (response.StatusCode, response.Headers.GetValues("Access-Control-Allow-Origin").Single()));
    }

    public void Dispose() => api.Dispose();

    [GeneratedRegex("^[BCDFGHJKLMNPQRSTVWXZ]{4}-[BCDFGHJKLMNPQRSTVWXZ]{4}$")]
    private static partial Regex CodePattern();

    private ChannelHandle Handles() => factory.Services.GetRequiredService<ChannelHandle>();
}
