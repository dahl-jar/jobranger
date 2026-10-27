using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jobranger.Relay.Tests;

public sealed class TimestampTests : IClassFixture<RelayFactory>, IDisposable {
    private static readonly TimeSpan fraction = TimeSpan.FromMilliseconds(750);

    private readonly RelayFactory factory;
    private readonly RelayApi api;

    public TimestampTests(RelayFactory factory) {
        this.factory = factory;
        api = RelayApi.For(factory, Addresses.Next());
        AdvanceToFraction();
    }

    [Fact]
    public async Task ShouldReportCliHandleExpiryEqualToTokenExpiry() {
        using CliKey key = new();

        JsonObject started = await api.StartPairing(key);

        Assert.Equal(TokenExpiry(started["handle"]), (DateTimeOffset)started["handleExpiresAt"]!);
    }

    [Fact]
    public async Task ShouldReportWebHandleExpiryEqualToTokenExpiry() {
        using CliKey key = new();

        JsonObject claimed = await api.Paired(key);

        Assert.Equal(TokenExpiry(claimed["handle"]), (DateTimeOffset)claimed["handleExpiresAt"]!);
    }

    [Fact]
    public async Task ShouldReportTicketExpiryEqualToTokenExpiry() {
        using CliKey key = new();

        JsonObject claimed = await api.Paired(key);

        Assert.Equal(TokenExpiry(claimed["ticket"]), (DateTimeOffset)claimed["ticketExpiresAt"]!);
    }

    [Fact]
    public async Task ShouldReportCliSessionHandleExpiryEqualToTokenExpiry() {
        using CliKey key = new();

        using HttpResponseMessage response = await api.Post("/session/cli", key.Proof(CliKey.SessionCli, await api.Nonce()));

        JsonObject body = await RelayApi.Json(response);
        Assert.Equal(TokenExpiry(body["handle"]), (DateTimeOffset)body["handleExpiresAt"]!);
    }

    [Fact]
    public async Task ShouldReportCodeExpiryRoundedDownToWholeSecond() {
        using CliKey key = new();
        DateTimeOffset expires = factory.Time.GetUtcNow() + TimeSpan.FromMinutes(10);

        JsonObject started = await api.StartPairing(key);

        Assert.Equal(WholeSeconds(expires), (DateTimeOffset)started["expiresAt"]!);
    }

    [Fact]
    public async Task ShouldReportNonceExpiryRoundedDownToWholeSecond() {
        DateTimeOffset expires = factory.Time.GetUtcNow() + TimeSpan.FromSeconds(60);

        using HttpResponseMessage response = await api.Post("/pair/nonce", RelayApi.NonceBody(null));

        JsonObject body = await RelayApi.Json(response);

        Assert.Equal(WholeSeconds(expires), (DateTimeOffset)body["expiresAt"]!);
    }

    public void Dispose() => api.Dispose();

    private void AdvanceToFraction() {
        long intoSecond = factory.Time.GetUtcNow().Ticks % TimeSpan.TicksPerSecond;
        long ahead = (fraction.Ticks - intoSecond + TimeSpan.TicksPerSecond) % TimeSpan.TicksPerSecond;
        factory.Time.Advance(TimeSpan.FromTicks(ahead));
    }

    private static DateTimeOffset WholeSeconds(DateTimeOffset value) =>
        DateTimeOffset.FromUnixTimeSeconds(value.ToUnixTimeSeconds());

    private static DateTimeOffset TokenExpiry(JsonNode? token) {
        string body = ((string)token!).Split('.')[0];
        using JsonDocument payload = JsonDocument.Parse(Base64Url.DecodeFromChars(body));
        return DateTimeOffset.FromUnixTimeSeconds(payload.RootElement.GetProperty("E").GetInt64());
    }
}
