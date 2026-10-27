using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests;

public sealed class ChannelRegistryTests : IDisposable {
    private const string cli = "cli-one";
    private const string otherCli = "cli-two";
    private const string browser = "web-one";
    private const string otherBrowser = "web-two";
    private const string sid = "sid-one";
    private const string otherSid = "sid-two";
    private const string grant = "grant-one";
    private const string otherGrant = "grant-two";
    private const int browsersInFullChannel = 7;
    private const int sidsPerConnection = 32;
    private const int channelBytesBurst = 524_288;

    private readonly ChannelRegistry registry = new(new FakeTimeProvider());

    [Fact]
    public void ShouldKeepCliSlotFreeWhenBrowsersFillChannel() {
        for (int index = 0; index < browsersInFullChannel; index++) {
            registry.TryJoin(Channels.One, RelayRole.Web, $"web-{index}", GrantOf($"web-{index}"));
        }

        JoinResult joined = registry.TryJoin(Channels.One, RelayRole.Web, "web-extra", GrantOf("web-extra"));

        Assert.Equal(JoinOutcome.ChannelFull, joined.Outcome);
    }

    [Fact]
    public void ShouldReportOfflineForUnknownChannel() {
        Presence presence = registry.Snapshot(Channels.Three);

        Assert.Equal(new Presence(false, 0), presence);
    }

    [Fact]
    public void ShouldReportNothingWhenConnectionLeavesUnknownChannel() {
        Departure? departure = registry.Leave(Channels.Three, RelayRole.Web, browser);

        Assert.Null(departure);
    }

    [Fact]
    public void ShouldRefuseSidFromConnectionOutsideChannel() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);

        SidOutcome bound = registry.BindSid(Channels.One, browser, sid);

        Assert.Equal(SidOutcome.Taken, bound);
    }

    [Fact]
    public void ShouldForgetChannelAfterLastSocketLeaves() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        Assert.True(registry.TryConsumeBytes(Channels.One, RelayRole.Cli, channelBytesBurst));
        registry.Leave(Channels.One, RelayRole.Cli, cli);
        registry.TryJoin(Channels.One, RelayRole.Cli, otherCli, null);

        bool consumed = registry.TryConsumeBytes(Channels.One, RelayRole.Cli, channelBytesBurst);

        Assert.True(consumed);
    }

    [Fact]
    public void ShouldReportPresenceAfterBrowserLeaves() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        registry.TryJoin(Channels.One, RelayRole.Web, browser, GrantOf(browser));
        registry.TryJoin(Channels.One, RelayRole.Web, otherBrowser, GrantOf(otherBrowser));

        Departure? departure = registry.Leave(Channels.One, RelayRole.Web, browser);

        Assert.Equal(new Presence(true, 1), departure?.Presence);
    }

    [Fact]
    public void ShouldReplaceEarlierCliConnectionInFullChannel() {
        Fill(Channels.One);

        JoinResult joined = registry.TryJoin(Channels.One, RelayRole.Cli, otherCli, null);

        Assert.Equal(new JoinResult(JoinOutcome.Joined, cli), joined);
    }

    [Fact]
    public void ShouldKeepCliOnlineWhenReplacedConnectionLeaves() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        registry.TryJoin(Channels.One, RelayRole.Cli, otherCli, null);
        registry.Leave(Channels.One, RelayRole.Cli, cli);

        Presence presence = registry.Snapshot(Channels.One);

        Assert.Equal(new Presence(true, 0), presence);
    }

    [Fact]
    public void ShouldReportNothingWhenUnknownConnectionLeaves() {
        registry.TryJoin(Channels.One, RelayRole.Web, browser, GrantOf(browser));

        Departure? departure = registry.Leave(Channels.One, RelayRole.Web, otherBrowser);

        Assert.Null(departure);
    }

    [Fact]
    public void ShouldRefuseSidHeldByAnotherBrowser() {
        JoinBrowsers();
        registry.BindSid(Channels.One, browser, sid);

        SidOutcome bound = registry.BindSid(Channels.One, otherBrowser, sid);

        Assert.Equal(SidOutcome.Taken, bound);
    }

    [Fact]
    public void ShouldAcceptSidAgainFromItsBrowser() {
        JoinBrowsers();
        registry.BindSid(Channels.One, browser, sid);

        SidOutcome bound = registry.BindSid(Channels.One, browser, sid);

        Assert.Equal(SidOutcome.Bound, bound);
    }

    [Fact]
    public void ShouldRefuseSidBeyondPerConnectionLimit() {
        FillSids();

        SidOutcome bound = registry.BindSid(Channels.One, browser, "sid-extra");

        Assert.Equal(SidOutcome.TooMany, bound);
    }

    [Fact]
    public void ShouldReturnBrowserSidsOnLeave() {
        JoinBrowsers();
        registry.BindSid(Channels.One, browser, sid);
        registry.BindSid(Channels.One, browser, otherSid);

        Departure? departure = registry.Leave(Channels.One, RelayRole.Web, browser);

        Assert.Equal([sid, otherSid], departure?.Sids);
    }

    [Fact]
    public void ShouldFreeSidForOtherBrowserAfterLeave() {
        JoinBrowsers();
        registry.BindSid(Channels.One, browser, sid);
        registry.Leave(Channels.One, RelayRole.Web, browser);

        SidOutcome bound = registry.BindSid(Channels.One, otherBrowser, sid);

        Assert.Equal(SidOutcome.Bound, bound);
    }

    [Fact]
    public void ShouldFreeBrowserSidsWhenCliLeaves() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        FillSids();
        registry.Leave(Channels.One, RelayRole.Cli, cli);

        SidOutcome bound = registry.BindSid(Channels.One, browser, "sid-extra");

        Assert.Equal(SidOutcome.Bound, bound);
    }

    [Fact]
    public void ShouldLetOtherBrowserTakeSidAfterCliLeaves() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        JoinBrowsers();
        registry.BindSid(Channels.One, browser, sid);
        registry.Leave(Channels.One, RelayRole.Cli, cli);

        SidOutcome bound = registry.BindSid(Channels.One, otherBrowser, sid);

        Assert.Equal(SidOutcome.Bound, bound);
    }

    [Fact]
    public void ShouldFreeBrowserSidsWhenCliIsReplaced() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        FillSids();
        registry.TryJoin(Channels.One, RelayRole.Cli, otherCli, null);

        SidOutcome bound = registry.BindSid(Channels.One, browser, "sid-extra");

        Assert.Equal(SidOutcome.Bound, bound);
    }

    [Fact]
    public void ShouldRefuseBytesBeyondChannelBurst() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        Assert.True(registry.TryConsumeBytes(Channels.One, RelayRole.Cli, channelBytesBurst));

        bool consumed = registry.TryConsumeBytes(Channels.One, RelayRole.Cli, 1);

        Assert.False(consumed);
    }

    public void Dispose() => registry.Dispose();

    [Fact]
    public void ShouldKeepCliByteBudgetWhenBrowsersUseTheirs() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        Assert.True(registry.TryConsumeBytes(Channels.One, RelayRole.Web, channelBytesBurst));

        bool consumed = registry.TryConsumeBytes(Channels.One, RelayRole.Cli, channelBytesBurst);

        Assert.True(consumed);
    }

    [Fact]
    public void ShouldPredictRefusalWithoutJoining() {
        Fill(Channels.One);

        JoinOutcome predicted = registry.Admits(Channels.One, RelayRole.Web, otherGrant);

        Assert.Equal((JoinOutcome.ChannelFull, new Presence(true, browsersInFullChannel)), (predicted, registry.Snapshot(Channels.One)));
    }

    [Theory]
    [InlineData(RelayRole.Cli)]
    [InlineData(RelayRole.Web)]
    public void ShouldPredictJoinForChannelWithRoom(RelayRole role) {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);

        JoinOutcome predicted = registry.Admits(Channels.One, role, role == RelayRole.Web ? grant : null);

        Assert.Equal(JoinOutcome.Joined, predicted);
    }

    [Fact]
    public void ShouldListConnectionsOfOneRole() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        JoinBrowsers();

        IReadOnlyList<string> browsers = registry.Members(Channels.One, RelayRole.Web);

        Assert.Equal([browser, otherBrowser], browsers);
    }

    [Fact]
    public void ShouldListCliConnection() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);
        JoinBrowsers();

        IReadOnlyList<string> clis = registry.Members(Channels.One, RelayRole.Cli);

        Assert.Equal([cli], clis);
    }

    [Fact]
    public void ShouldListNoMembersForUnknownChannel() {
        IReadOnlyList<string> members = registry.Members(Channels.Three, RelayRole.Web);

        Assert.Empty(members);
    }

    [Fact]
    public void ShouldRefuseThirdSocketOfOneTicket() {
        registry.TryJoin(Channels.One, RelayRole.Web, browser, grant);
        registry.TryJoin(Channels.One, RelayRole.Web, otherBrowser, grant);

        (JoinOutcome, JoinOutcome) joined = (
            registry.TryJoin(Channels.One, RelayRole.Web, "web-three", grant).Outcome,
            registry.TryJoin(Channels.One, RelayRole.Web, "web-four", otherGrant).Outcome);

        Assert.Equal((JoinOutcome.TicketFull, JoinOutcome.Joined), joined);
    }

    [Fact]
    public void ShouldFreeTicketSlotWhenItsSocketLeaves() {
        registry.TryJoin(Channels.One, RelayRole.Web, browser, grant);
        registry.TryJoin(Channels.One, RelayRole.Web, otherBrowser, grant);
        registry.Leave(Channels.One, RelayRole.Web, browser);

        JoinResult joined = registry.TryJoin(Channels.One, RelayRole.Web, "web-three", grant);

        Assert.Equal(JoinOutcome.Joined, joined.Outcome);
    }

    [Fact]
    public void ShouldReportFullTicketWithoutJoining() {
        registry.TryJoin(Channels.One, RelayRole.Web, browser, grant);
        registry.TryJoin(Channels.One, RelayRole.Web, otherBrowser, grant);

        JoinOutcome predicted = registry.Admits(Channels.One, RelayRole.Web, grant);

        Assert.Equal(JoinOutcome.TicketFull, predicted);
    }

    [Fact]
    public void ShouldRefuseBrowserJoinWithoutGrant() {
        registry.TryJoin(Channels.One, RelayRole.Cli, cli, null);

        JoinResult joined = registry.TryJoin(Channels.One, RelayRole.Web, browser, null);

        Assert.Equal(JoinOutcome.TicketFull, joined.Outcome);
    }

    private static string GrantOf(string connectionId) => $"grant-{connectionId}";

    private void Fill(ChannelId channel) {
        Assert.Equal(JoinOutcome.Joined, registry.TryJoin(channel, RelayRole.Cli, cli, null).Outcome);
        for (int index = 0; index < browsersInFullChannel; index++) {
            Assert.Equal(JoinOutcome.Joined, registry.TryJoin(channel, RelayRole.Web, $"web-{index}", GrantOf($"web-{index}")).Outcome);
        }
    }

    private void FillSids() {
        JoinBrowsers();
        for (int index = 0; index < sidsPerConnection; index++) {
            Assert.Equal(SidOutcome.Bound, registry.BindSid(Channels.One, browser, $"sid-{index}"));
        }
    }

    private void JoinBrowsers() {
        registry.TryJoin(Channels.One, RelayRole.Web, browser, GrantOf(browser));
        registry.TryJoin(Channels.One, RelayRole.Web, otherBrowser, GrantOf(otherBrowser));
    }
}
