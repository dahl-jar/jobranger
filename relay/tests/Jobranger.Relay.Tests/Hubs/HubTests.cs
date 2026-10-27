using System.Text.Json.Nodes;
using Jobranger.Contracts.Channels;
using Jobranger.Relay.Core.Tokens;
using Jobranger.Relay.Hubs;
using Jobranger.Relay.Tests.Mocks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Relay.Tests.Hubs;

public sealed class HubTests(RelayFactory factory) : IClassFixture<RelayFactory> {
    private const int messagesBurst = 40;
    private const int sidsPerConnection = 32;
    private const int maxCiphertextChars = 61_440;
    private const int framesInByteBurst = 8;
    private const int oversizeCiphertextChars = 66_000;

    private static readonly TimeSpan silence = TimeSpan.FromHours(1);
    private static readonly TimeSpan clientTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan beforeTimeout = TimeSpan.FromSeconds(20);

    private static readonly Dictionary<string, Func<JsonObject, string>> brokenEnvelopes = new(StringComparer.Ordinal) {
        ["duplicate-property"] = envelope => envelope.ToJsonString().Insert(1, "\"seq\":2,"),
        ["property-name-case"] = envelope => envelope.ToJsonString().Replace("\"seq\":", "\"Seq\":", StringComparison.Ordinal),
    };

    [Fact]
    public async Task ShouldForwardEnvelopeToOtherRoleOnly() {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        using CliKey key = new();
        JsonObject started = await api.StartPairing(key);
        using HttpResponseMessage claimed = await api.Claim((string)started["code"]!);
        string webHandle = (string)(await RelayApi.Json(claimed))["handle"]!;
        ChannelId channel = new((string)started["channel"]!);
        await using HubPeer cli = await RelayClients.ConnectAsync(factory, (string)started["handle"]!, null);
        await using HubPeer browser = await RelayClients.ConnectAsync(factory, webHandle, RelayFactory.SiteOrigin);
        await using HubPeer sibling = await RelayClients.ConnectAsync(factory, webHandle, RelayFactory.SiteOrigin);
        string sid = Envelopes.NewSid();
        string fromBrowser = Envelopes.Data(channel, sid).ToJsonString();
        string fromCli = Envelopes.Hello(channel, sid).ToJsonString();
        await browser.Send(fromBrowser);
        await cli.Send(fromCli);

        (string, string, string) received = (await cli.Next(HubPeer.Receive), await browser.Next(HubPeer.Receive), await sibling.Next(HubPeer.Receive));

        Assert.Equal((Envelopes.Compact(fromBrowser), Envelopes.Compact(fromCli), Envelopes.Compact(fromCli)), received);
    }

    [Fact]
    public async Task ShouldSendPresenceWhenCliConnects() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        await using HubPeer cli = await RelayClients.ConnectCliAsync(factory, channel);

        string presence = await browser.Next(HubPeer.Presence);

        Assert.Equal("""{"cli_online":true,"browsers":1}""", presence);
    }

    [Fact]
    public async Task ShouldSendPresenceWhenBrowserLeaves() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer cli = await RelayClients.ConnectCliAsync(factory, channel);
        HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        await cli.Next(HubPeer.Presence);
        await browser.DisposeAsync();

        string presence = await cli.Next(HubPeer.Presence);

        Assert.Equal("""{"cli_online":true,"browsers":0}""", presence);
    }

    [Fact]
    public async Task ShouldRejectEnvelopeWithUnknownMajorVersion() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        JsonObject envelope = Envelopes.WithUnsupportedVersion(channel);

        string reason = await browser.Rejection(envelope.ToJsonString());

        Assert.Equal("unsupported_version", reason);
    }

    [Theory]
    [InlineData("duplicate-property")]
    [InlineData("property-name-case")]
    public async Task ShouldRejectEnvelopeBreakingContract(string broken) {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);

        string reason = await browser.Rejection(brokenEnvelopes[broken](Envelopes.Data(channel, Envelopes.NewSid())));

        Assert.Equal("invalid_envelope", reason);
    }

    [Fact]
    public async Task ShouldNotLogRejectedEnvelopeAsFailure() {
        await using RelayFactory own = new();
        ChannelId channel = RelayClients.NewChannel();
        await using (HubPeer browser = await RelayClients.ConnectWebAsync(own, channel)) {
            Assert.Equal("invalid_envelope", await browser.Rejection("""{"v":1}"""));
        }

        IReadOnlyList<FakeLogRecord> records = own.Logs.GetSnapshot();

        Assert.DoesNotContain(records, record => record.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ShouldRejectEnvelopeForAnotherChannel() {
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, RelayClients.NewChannel());

        string reason = await browser.Rejection(Envelopes.Data(RelayClients.NewChannel(), Envelopes.NewSid()).ToJsonString());

        Assert.Equal("channel_mismatch", reason);
    }

    [Fact]
    public async Task ShouldRejectEnvelopeOverMessageRate() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        string envelope = Envelopes.Data(channel, Envelopes.NewSid()).ToJsonString();
        for (int message = 0; message < messagesBurst; message++) {
            await browser.Send(envelope);
        }

        string reason = await browser.Rejection(envelope);

        Assert.Equal("rate_limited", reason);
    }

    [Fact]
    public async Task ShouldRejectEnvelopeOverChannelByteRate() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer cli = await RelayClients.ConnectCliAsync(factory, channel);
        string envelope = With(Envelopes.Data(channel, Envelopes.NewSid()), "ciphertext", new string('A', maxCiphertextChars));
        for (int frame = 0; frame < framesInByteBurst; frame++) {
            await cli.Send(envelope);
        }

        string reason = await cli.Rejection(envelope);

        Assert.Equal("rate_limited", reason);
    }

    [Fact]
    public async Task ShouldCloseSocketOnFrameOver64KiB() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer cli = await RelayClients.ConnectCliAsync(factory, channel);
        Task send = cli.Send(With(Envelopes.Data(channel, Envelopes.NewSid()), "ciphertext", new string('A', oversizeCiphertextChars)));

        await cli.Closed;

        await Assert.ThrowsAnyAsync<Exception>(() => send);
    }

    [Fact]
    public async Task ShouldRejectBrowserEnvelopeWithSidOfAnotherBrowser() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        await using HubPeer sibling = await RelayClients.ConnectWebAsync(factory, channel);
        string sid = Envelopes.NewSid();
        await browser.Send(Envelopes.Hello(channel, sid).ToJsonString());

        string reason = await sibling.Rejection(Envelopes.Hello(channel, sid).ToJsonString());

        Assert.Equal("sid_in_use", reason);
    }

    [Fact]
    public async Task ShouldNotTellCliWhenBrowserWithoutSessionsCloses() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer cli = await RelayClients.ConnectCliAsync(factory, channel);
        HubPeer idle = await RelayClients.ConnectWebAsync(factory, channel);
        await cli.Next(HubPeer.Presence);
        await idle.DisposeAsync();
        await cli.Next(HubPeer.Presence);
        HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        string sid = Envelopes.NewSid();
        await browser.Send(Envelopes.Hello(channel, sid).ToJsonString());
        await browser.DisposeAsync();

        string notice = await cli.Next(HubPeer.SessionsClosed);

        Assert.Equal($$"""{"sids":["{{sid}}"]}""", notice);
    }

    [Fact]
    public async Task ShouldTellReplacedCliBeforeClosingIt() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer first = await RelayClients.ConnectCliAsync(factory, channel);
        await using HubPeer second = await RelayClients.ConnectCliAsync(factory, channel);

        await first.WasReplaced();

        Assert.Equal(string.Empty, await first.Closed);
    }

    [Fact]
    public async Task ShouldShowBrowsersCliGoingOfflineWhenReplaced() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        await using HubPeer first = await RelayClients.ConnectCliAsync(factory, channel);
        await browser.Next(HubPeer.Presence);
        await using HubPeer second = await RelayClients.ConnectCliAsync(factory, channel);

        (string, string) presence = (await browser.Next(HubPeer.Presence), await browser.Next(HubPeer.Presence));

        Assert.Equal(("""{"cli_online":false,"browsers":1}""", """{"cli_online":true,"browsers":1}"""), presence);
    }

    [Fact]
    public async Task ShouldRefuseThirdSocketOfOneTicket() {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        using CliKey key = new();
        JsonObject paired = await api.Paired(key);
        using HttpResponseMessage refreshed = await api.WebSession((string)paired["ticket"]!);
        string laterHandle = (string)(await RelayApi.Json(refreshed))["handle"]!;
        ChannelId channel = new((string)paired["channel"]!);
        await using HubPeer first = await RelayClients.ConnectAsync(factory, (string)paired["handle"]!, RelayFactory.SiteOrigin);
        await using HubPeer second = await RelayClients.ConnectAsync(factory, laterHandle, RelayFactory.SiteOrigin);
        await using HubPeer third = RelayClients.Build(factory, laterHandle, RelayFactory.SiteOrigin);
        await third.Connection.StartAsync(TestContext.Current.CancellationToken);

        string reason = await third.Closed;

        await using HubPeer otherTicket = await RelayClients.ConnectAsync(
            factory, RelayClients.Handle(factory, channel, RelayRole.Web), RelayFactory.SiteOrigin);
        Assert.Equal(("ticket_full", HubConnectionState.Connected), (reason, otherTicket.Connection.State));
    }

    [Fact]
    public async Task ShouldRejectBrowserEnvelopeBeyondSessionLimit() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        for (int session = 0; session < sidsPerConnection; session++) {
            await browser.Send(Envelopes.Hello(channel, Envelopes.NewSid()).ToJsonString());
        }

        string reason = await browser.Rejection(Envelopes.Hello(channel, Envelopes.NewSid()).ToJsonString());

        Assert.Equal("too_many_sessions", reason);
    }

    [Fact]
    public async Task ShouldAdmitCliHandleForChannelUnknownToRelay() {
        await using HubPeer cli = await RelayClients.ConnectCliAsync(factory, RelayClients.NewChannel());

        Assert.Equal(HubConnectionState.Connected, cli.Connection.State);
    }

    [Fact]
    public async Task ShouldReportDecimalVersionAsInvalidEnvelope() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        string envelope = Envelopes.Data(channel, Envelopes.NewSid()).ToJsonString().Replace("\"v\":1,", "\"v\":1.0,", StringComparison.Ordinal);

        string reason = await browser.Rejection(envelope);

        Assert.Equal("invalid_envelope", reason);
    }

    [Fact]
    public async Task ShouldCloseSilentBrowserAndEndItsSessions() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer cli = await RelayClients.ConnectCliAsync(factory, channel);
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel, silence);
        string sid = Envelopes.NewSid();
        await browser.Send(Envelopes.Hello(channel, sid).ToJsonString());
        await cli.Next(HubPeer.Receive);
        factory.Time.Advance(beforeTimeout);
        await cli.Send(Envelopes.Data(channel, sid).ToJsonString());
        factory.Time.Advance(clientTimeout - beforeTimeout);

        string notice = await cli.Next(HubPeer.SessionsClosed);

        Assert.Equal(($$"""{"sids":["{{sid}}"]}""", HubConnectionState.Connected), (notice, cli.Connection.State));
    }

    private static string With(JsonObject envelope, string property, JsonNode value) {
        envelope[property] = value;
        return envelope.ToJsonString();
    }
}
