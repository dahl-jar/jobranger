using System.Net;
using System.Text;
using Jobranger.Relay.Tests.Mocks;

namespace Jobranger.Relay.Tests.Security;

public sealed class BadBodyLimitTests(RelayFactory factory) : IClassFixture<RelayFactory> {
    private const int noncesPerAddress = 30;
    private const int failedSessionsPerAddress = 120;
    private const string malformedJson = "{\"v\":1,";
    private const string unsupportedVersion = "{\"v\":2}";
    private const string missingVersion = "{}";
    private const string oversized = "oversized";
    private const string wrongType = "wrong-type";
    private const string plainText = "text/plain";
    private const int oversizedBodyBytes = (int)BodyLimitFilter.MaxBodyBytes + 1;

    [Theory]
    [InlineData("/pair/nonce", malformedJson, noncesPerAddress)]
    [InlineData("/pair/nonce", unsupportedVersion, noncesPerAddress)]
    [InlineData("/session/cli", malformedJson, failedSessionsPerAddress)]
    [InlineData("/session/web", missingVersion, failedSessionsPerAddress)]
    [InlineData("/pair/nonce", oversized, noncesPerAddress)]
    [InlineData("/session/cli", oversized, failedSessionsPerAddress)]
    [InlineData("/pair/nonce", wrongType, noncesPerAddress)]
    [InlineData("/session/cli", wrongType, failedSessionsPerAddress)]
    public async Task ShouldRefuseBadBodiesPastAddressLimit(string path, string kind, int limit) {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        for (int attempt = 1; attempt < limit; attempt++) {
            using HttpResponseMessage rejected = await Send(api, path, kind);
        }

        using HttpResponseMessage last = await Send(api, path, kind);
        using HttpResponseMessage refused = await Send(api, path, kind);

        Assert.Equal((Rejection(kind), HttpStatusCode.TooManyRequests), (last.StatusCode, refused.StatusCode));
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    private static HttpStatusCode Rejection(string kind) => kind switch {
        oversized => HttpStatusCode.RequestEntityTooLarge,
        wrongType => HttpStatusCode.UnsupportedMediaType,
        _ => HttpStatusCode.BadRequest,
    };

    private static async Task<HttpResponseMessage> Send(RelayApi api, string path, string kind) {
        switch (kind) {
            case oversized:
                return await api.PostRaw(path, $"{{\"v\":1,\"pad\":\"{new string('a', oversizedBodyBytes)}\"}}");
            case wrongType:
                using (HttpRequestMessage request = new(HttpMethod.Post, path) { Content = new StringContent("{\"v\":1}", Encoding.UTF8, plainText) }) {
                    return await api.Send(request);
                }

            default:
                return await api.PostRaw(path, kind);
        }
    }
}
