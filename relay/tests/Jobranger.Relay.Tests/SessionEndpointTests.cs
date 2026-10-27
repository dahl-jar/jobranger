using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Jobranger.Relay.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jobranger.Relay.Tests;

public sealed class SessionEndpointTests(RelayFactory factory) : IClassFixture<RelayFactory>, IDisposable
{
    private static readonly TimeSpan handleLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ticketLifetime = TimeSpan.FromDays(90);
    private static readonly TimeSpan oneDay = TimeSpan.FromDays(1);

    private readonly RelayApi api = RelayApi.For(factory, Addresses.Next());

    [Fact]
    public async Task ShouldReturnCliHandleForSessionProof()
    {
        using CliKey key = new();
        JsonObject started = await api.StartPairing(key);
        string channel = (string)started["channel"]!;

        using HttpResponseMessage response = await api.Post("/session/cli", key.Proof(CliKey.SessionCli, await api.Nonce()));

        JsonObject body = await RelayApi.Json(response);
        Assert.Equal(
            (HttpStatusCode.OK, channel, factory.Time.GetUtcNow() + handleLifetime),
            (response.StatusCode, (string)body["channel"]!, (DateTimeOffset)body["handleExpiresAt"]!));
        Assert.Equal(new HandleClaims(new ChannelId(channel), RelayRole.Cli), Handles().Verify((string)body["handle"]!));
    }

    [Fact]
    public async Task ShouldRejectSessionProofSignedForPairStart()
    {
        using CliKey key = new();

        using HttpResponseMessage response = await api.Post("/session/cli", key.Proof(CliKey.PairStart, await api.Nonce()));

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_signature"), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldExchangeTicketForNewHandleAndLaterTicket()
    {
        using CliKey key = new();
        JsonObject paired = await api.Paired(key);
        string ticket = (string)paired["ticket"]!;
        factory.Time.Advance(oneDay);
        DateTimeOffset now = factory.Time.GetUtcNow();

        using HttpResponseMessage response = await api.WebSession(ticket);

        JsonObject body = await RelayApi.Json(response);
        ChannelId channel = new((string)paired["channel"]!);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(channel.Value, (string)body["channel"]!);
        Assert.Equal(new HandleClaims(channel, RelayRole.Web), Handles().Verify((string)body["handle"]!));
        Assert.Equal(now + handleLifetime, (DateTimeOffset)body["handleExpiresAt"]!);
        Assert.Equal(now + ticketLifetime, (DateTimeOffset)body["ticketExpiresAt"]!);
        Assert.Equal(new TicketCheck(TicketOutcome.Valid, channel), Handles().VerifyTicket((string)body["ticket"]!));
    }

    [Fact]
    public async Task ShouldReturn410ForExpiredTicket()
    {
        using CliKey key = new();
        JsonObject paired = await api.Paired(key);
        string ticket = (string)paired["ticket"]!;
        factory.Time.Advance(ticketLifetime);

        using HttpResponseMessage response = await api.WebSession(ticket);

        Assert.Equal((HttpStatusCode.Gone, "ticket_expired"), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldReturn400ForInvalidTicket()
    {
        using CliKey key = new();
        JsonObject paired = await api.Paired(key);
        string handle = (string)paired["handle"]!;

        using HttpResponseMessage response = await api.WebSession(handle);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_ticket"), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldAcceptTicketSignedWithPreviousKey()
    {
        byte[] previousKey = Convert.FromBase64String(RelayFactory.HandleKey);
        using var rotated = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Relay:HandleKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            builder.UseSetting("Relay:PreviousHandleKey", RelayFactory.HandleKey);
        });
        using RelayApi rotatedApi = RelayApi.For(rotated, Addresses.Next());
        ChannelHandle previous = new(new HandleKeys(previousKey, null), factory.Time);
        string ticket = previous.SignTicket(new ChannelId("channel-one"), factory.Time.GetUtcNow() + ticketLifetime);

        using HttpResponseMessage response = await rotatedApi.WebSession(ticket);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose() => api.Dispose();

    private ChannelHandle Handles() => factory.Services.GetRequiredService<ChannelHandle>();
}
