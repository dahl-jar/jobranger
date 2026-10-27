using System.Net;
using Jobranger.Relay.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Relay.Tests;

public sealed class HubAuthTests(RelayFactory factory) : IClassFixture<RelayFactory>
{
    private const string foreignOrigin = "https://other.test";
    private const int socketsPerAddress = 10;
    private const int browsersInFullChannel = 7;
    private const int failedAttemptsPerWindow = 120;
    private const int sampledAttempts = 3;
    private const int requestsBeyondBlockShare = 60;
    private const int smallHubSocketCap = 2;
    private const int socketsPerNetwork = 16;

    [Fact]
    public async Task ShouldRefuseSocketWithoutHandle()
    {
        await using HubPeer peer = RelayClients.Build(factory, null, null);

        string refusal = await Refusal(peer);

        Assert.Contains("401", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRefuseCliSocketFromForeignOrigin()
    {
        string handle = RelayClients.Handle(factory, RelayClients.NewChannel(factory), RelayRole.Cli);
        await using HubPeer peer = RelayClients.Build(factory, handle, foreignOrigin);

        string refusal = await Refusal(peer);

        Assert.Contains("401", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRefuseBrowserSocketWithoutOrigin()
    {
        string handle = RelayClients.Handle(factory, RelayClients.NewChannel(factory), RelayRole.Web);
        await using HubPeer peer = RelayClients.Build(factory, handle, null);

        string refusal = await Refusal(peer);

        Assert.Contains("401", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRefuseEleventhHubSocketFromOneAddress()
    {
        string address = Addresses.Next();
        List<HubPeer> open = [];
        for (int socket = 0; socket < socketsPerAddress; socket++)
        {
            open.Add(await RelayClients.ConnectCliAsync(factory, RelayClients.NewChannel(factory), address));
        }

        string handle = RelayClients.Handle(factory, RelayClients.NewChannel(factory), RelayRole.Cli);
        await using HubPeer extra = RelayClients.Build(factory, handle, null, address);

        string refusal = await Refusal(extra);

        Assert.Contains("429", refusal, StringComparison.Ordinal);
        foreach (HubPeer peer in open)
        {
            await peer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ShouldRefuseBrowserBeyondChannelLimitWithReason()
    {
        ChannelId channel = RelayClients.NewChannel(factory);
        List<HubPeer> open = [await RelayClients.ConnectCliAsync(factory, channel)];
        for (int browser = 0; browser < browsersInFullChannel; browser++)
        {
            open.Add(await RelayClients.ConnectWebAsync(factory, channel));
        }

        await using HubPeer extra = RelayClients.Build(factory, RelayClients.Handle(factory, channel, RelayRole.Web), RelayFactory.SiteOrigin);
        await extra.Connection.StartAsync(TestContext.Current.CancellationToken);

        string reason = await extra.Closed;

        Assert.Equal("channel_full", reason);

        foreach (HubPeer peer in open)
        {
            await peer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ShouldNotLogHubAccessToken()
    {
        ChannelId channel = RelayClients.NewChannel(factory);
        string cliHandle = RelayClients.Handle(factory, channel, RelayRole.Cli);
        string webHandle = RelayClients.Handle(factory, channel, RelayRole.Web);
        await using (HubPeer cli = await RelayClients.ConnectAsync(factory, cliHandle, null))
        await using (HubPeer browser = await RelayClients.ConnectAsync(factory, webHandle, RelayFactory.SiteOrigin))
        {
            await browser.Send(Envelopes.Hello(channel, Envelopes.NewSid()).ToJsonString());
            await cli.Next(HubPeer.Receive);
        }

        IReadOnlyList<FakeLogRecord> records = factory.Logs.GetSnapshot();

        string written = string.Join('\n', records.Select(record => $"{record.Message} {record.Exception}"));
        Assert.DoesNotContain(cliHandle, written, StringComparison.Ordinal);
        Assert.DoesNotContain(webHandle, written, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturn429AfterRepeatedHubAuthFailures()
    {
        string address = Addresses.Next();
        await FailAuth(address, failedAttemptsPerWindow);

        string refusal = await BadHandleRefusal(address);

        Assert.Contains("429", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReleaseHubSocketPermitsOfClosedRequests()
    {
        string block = Addresses.NextBlock();
        for (int network = 0; network < requestsBeyondBlockShare - 1; network++)
        {
            Assert.Contains("401", await BadHandleRefusal($"{block}:{network:x}::1"), StringComparison.Ordinal);
        }

        string refusal = await BadHandleRefusal($"{block}:ffff::1");

        Assert.Contains("401", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldChargeRefusedJoinToAddress()
    {
        string address = Addresses.Next();
        await FailAuth(address, failedAttemptsPerWindow - 1);
        await using (HubPeer unpaired = RelayClients.Build(
            factory, RelayClients.Handle(factory, RelayClients.UnpairedChannel(), RelayRole.Cli), null, address))
        {
            await unpaired.Connection.StartAsync(TestContext.Current.CancellationToken);
            Assert.Equal("unpaired", await unpaired.Closed);
        }

        string refusal = await BadHandleRefusal(address);

        Assert.Contains("429", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldChargeTicketFullJoinToAddress()
    {
        string address = Addresses.Next();
        ChannelId channel = RelayClients.NewChannel(factory);
        string handle = RelayClients.Handle(factory, channel, RelayRole.Web);
        await using HubPeer first = await RelayClients.ConnectAsync(factory, handle, RelayFactory.SiteOrigin);
        await using HubPeer second = await RelayClients.ConnectAsync(factory, handle, RelayFactory.SiteOrigin);
        await FailAuth(address, failedAttemptsPerWindow - 1);
        await using (HubPeer third = RelayClients.Build(factory, handle, RelayFactory.SiteOrigin, address))
        {
            await third.Connection.StartAsync(TestContext.Current.CancellationToken);
            Assert.Equal("ticket_full", await third.Closed);
        }

        string refusal = await BadHandleRefusal(address);

        Assert.Contains("429", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldLogOneSampledLineForRepeatedAuthFailures()
    {
        await using RelayFactory own = new();
        string address = Addresses.Next();
        for (int attempt = 0; attempt < sampledAttempts; attempt++)
        {
            await using HubPeer peer = RelayClients.Build(own, "not-a-handle", null, address);
            await Refusal(peer);
        }

        IReadOnlyList<FakeLogRecord> records = own.Logs.GetSnapshot();

        Assert.Equal(
            (0, 1),
            (records.Count(record => record.Category?.Contains("HandleAuthHandler", StringComparison.Ordinal) == true),
             records.Count(record => record.Message.StartsWith("hub auth refused", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task ShouldAnswer503RelayFullAtHubSocketCap()
    {
        await using RelayFactory small = SmallRelay();
        await using HubPeer first = await RelayClients.ConnectCliAsync(small, RelayClients.NewChannel(small));
        await using HubPeer second = await RelayClients.ConnectCliAsync(small, RelayClients.NewChannel(small));

        using HttpResponseMessage response = await Upgrade(small, Addresses.Next());

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "relay_full"), await RelayApi.Problem(response));
    }

    [Fact]
    public async Task ShouldTellClientWhenToRetryAtHubSocketCap()
    {
        await using RelayFactory small = SmallRelay();
        await using HubPeer first = await RelayClients.ConnectCliAsync(small, RelayClients.NewChannel(small));
        await using HubPeer second = await RelayClients.ConnectCliAsync(small, RelayClients.NewChannel(small));

        using HttpResponseMessage response = await Upgrade(small, Addresses.Next());

        Assert.Equal(TimeSpan.FromSeconds(30), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task ShouldRefuseNetworkAtItsHubSocketShare()
    {
        await using RelayFactory own = new();
        string block = Addresses.NextBlock();
        List<HubPeer> open = await FillNetwork(own, block);

        using HttpResponseMessage refused = await Upgrade(own, $"{block}:ff::1");
        (HttpStatusCode, string?) sameNetwork = await RelayApi.Problem(refused);

        await using HubPeer otherNetwork = await RelayClients.ConnectCliAsync(own, RelayClients.NewChannel(own), Addresses.Next());
        Assert.Equal(((HttpStatusCode.ServiceUnavailable, "relay_full"), HubConnectionState.Connected), (sameNetwork, otherNetwork.Connection.State));
        foreach (HubPeer peer in open)
        {
            await peer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ShouldReleaseNetworkShareWhenSocketCloses()
    {
        await using RelayFactory own = new();
        string block = Addresses.NextBlock();
        List<HubPeer> open = await FillNetwork(own, block);

        await open[0].DisposeAsync();
        await using HubPeer later = await RelayClients.ConnectCliAsync(own, RelayClients.NewChannel(own), $"{block}:ff::1");

        Assert.Equal(HubConnectionState.Connected, later.Connection.State);
        foreach (HubPeer peer in open.Skip(1))
        {
            await peer.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false, null, "invalid_handle")]
    [InlineData(true, foreignOrigin, "wrong_origin")]
    public async Task ShouldLogRefusalReasonForRejectedHubSocket(bool validHandle, string? origin, string reason)
    {
        await using RelayFactory own = new();
        string handle = validHandle ? RelayClients.Handle(own, RelayClients.NewChannel(own), RelayRole.Cli) : "not-a-handle";
        await using HubPeer peer = RelayClients.Build(own, handle, origin, Addresses.Next());
        await Refusal(peer);

        IReadOnlyList<FakeLogRecord> records = own.Logs.GetSnapshot();

        Assert.Contains(records, record => record.Message == $"hub auth refused reason={reason}");
    }

    [Fact]
    public async Task ShouldAdmitHubSocketAfterOneCloses()
    {
        await using RelayFactory small = SmallRelay();
        await using HubPeer first = await RelayClients.ConnectCliAsync(small, RelayClients.NewChannel(small));
        HubPeer second = await RelayClients.ConnectCliAsync(small, RelayClients.NewChannel(small));
        await second.DisposeAsync();

        await using HubPeer third = await RelayClients.ConnectCliAsync(small, RelayClients.NewChannel(small));

        Assert.Equal(HubConnectionState.Connected, third.Connection.State);
    }

    [Fact]
    public async Task ShouldNotServeNegotiate()
    {
        string handle = RelayClients.Handle(factory, RelayClients.NewChannel(factory), RelayRole.Cli);
        using RelayApi api = RelayApi.For(factory, Addresses.Next());

        using HttpResponseMessage response = await api.PostRaw($"/hub/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(handle)}", "");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static RelayFactory SmallRelay()
    {
        RelayFactory small = new();
        small.HubSocketCap = smallHubSocketCap;
        return small;
    }

    private static async Task<List<HubPeer>> FillNetwork(RelayFactory relay, string block)
    {
        List<HubPeer> open = [];
        for (int socket = 0; socket < socketsPerNetwork; socket++)
        {
            string address = $"{block}:{socket / socketsPerAddress:x}::1";
            open.Add(await RelayClients.ConnectCliAsync(relay, RelayClients.NewChannel(relay), address));
        }

        return open;
    }

    private static async Task<HttpResponseMessage> Upgrade(RelayFactory relay, string address)
    {
        using RelayApi api = RelayApi.For(relay, address);
        using HttpRequestMessage upgrade = HubUpgrade(RelayClients.Handle(relay, RelayClients.NewChannel(relay), RelayRole.Cli));
        return await api.Send(upgrade);
    }

    private static HttpRequestMessage HubUpgrade(string handle)
    {
        HttpRequestMessage upgrade = new(HttpMethod.Get, $"/hub?access_token={Uri.EscapeDataString(handle)}");
        upgrade.Headers.Connection.Add("Upgrade");
        upgrade.Headers.Upgrade.Add(new System.Net.Http.Headers.ProductHeaderValue("websocket"));
        upgrade.Headers.Add("Sec-WebSocket-Version", "13");
        upgrade.Headers.Add("Sec-WebSocket-Key", "dGhlIHNhbXBsZSBub25jZQ==");
        return upgrade;
    }

    private async Task FailAuth(string address, int attempts)
    {
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Assert.Contains("401", await BadHandleRefusal(address), StringComparison.Ordinal);
        }
    }

    private async Task<string> BadHandleRefusal(string address)
    {
        await using HubPeer peer = RelayClients.Build(factory, "not-a-handle", null, address);
        return await Refusal(peer);
    }

    private static async Task<string> Refusal(HubPeer peer)
    {
        Exception error = await Assert.ThrowsAnyAsync<Exception>(
            () => peer.Connection.StartAsync(TestContext.Current.CancellationToken));
        return error.Message;
    }
}
