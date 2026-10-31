using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Messaging.Limits;

namespace Jobranger.Cli.Tests.Features.Link;

public sealed class HubConnectorTests : IAsyncLifetime {
    private const string otherChannel = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string refusedHandle = "refused-handle-7f3c9a";
    private const int framesPastBurst = 60;

    private static readonly TimeSpan wait = TimeSpan.FromSeconds(15);

    private readonly RelayProcess relay = new();
    private readonly HttpClient http = new();
    private readonly LinkKeyPair keys = LinkKeys.Parse(LinkKeys.NewPair());
    private HubConnector connector = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        await relay.StartAsync();
        connector = new HubConnector(relay.Address, TimeProvider.System);
    }

    public async ValueTask DisposeAsync() {
        await relay.DisposeAsync();
        http.Dispose();
        keys.Dispose();
    }

    [Fact]
    public async Task ShouldReportHubReason_WhenRelayRefusesFrame() {
        await using IRelayConnection connection = await ConnectAsync();

        HubRefusedException refused = await Assert.ThrowsAsync<HubRefusedException>(
            () => connection.SendAsync(EnvelopeFixtures.Hello(otherChannel, EnvelopeFixtures.Sid), Token));

        Assert.Equal("channel_mismatch", refused.Reason);
    }

    [Fact]
    public async Task ShouldReportUnavailable_WhenSendingAfterRelayStops() {
        await using IRelayConnection connection = await ConnectAsync();
        await relay.StopAsync();
        while (await connection.ReceiveAsync(Token).AsTask().WaitAsync(wait, Token) is not RelayEvent.Disconnected) {
        }

        await Assert.ThrowsAsync<RelayUnavailableException>(
            () => connection.SendAsync(EnvelopeFixtures.Hello(keys.Channel.Value, EnvelopeFixtures.Sid), Token));
    }

    [Fact]
    public async Task ShouldRefuseLocally_WhenFrameIsOverRelayLimit() {
        await using IRelayConnection connection = await ConnectAsync();
        string ciphertext = new('A', MessageLimits.FrameMaxBytes);

        await Assert.ThrowsAsync<FrameTooLargeException>(
            () => connection.SendAsync(EnvelopeFixtures.Data(keys.Channel.Value, EnvelopeFixtures.Sid, 1, ciphertext), Token));
    }

    [Fact]
    public async Task ShouldStayUnderRelayRate_WhenSendingSixtyFrames() {
        await using IRelayConnection connection = await ConnectAsync();

        Task sending = Task.WhenAll(Enumerable.Range(1, framesPastBurst)
            .Select(seq => connection.SendAsync(
                EnvelopeFixtures.Data(keys.Channel.Value, EnvelopeFixtures.Sid, seq, EnvelopeFixtures.EmptySealed), Token)));

        await sending.WaitAsync(wait, Token);
    }

    [Fact]
    public async Task ShouldKeepHandleOutOfError_WhenRelayRejectsHandle() {
        RelayUnavailableException refused =
            await Assert.ThrowsAsync<RelayUnavailableException>(() => connector.ConnectAsync(refusedHandle, Token));

        Assert.DoesNotContain(refusedHandle, refused.ToString(), StringComparison.Ordinal);
    }

    private async Task<IRelayConnection> ConnectAsync() {
        RelayApiClient api = new(http, relay.Address);
        return await connector.ConnectAsync(await api.CliHandleAsync(keys.Signing, Token), Token);
    }
}
