using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Features.Link;

public sealed class HubConnectionFailureTests {
    private const string handle = "cli-handle";
    private const string channel = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string receiveMethod = "Receive";
    private const string sessionsClosedMethod = "SessionsClosed";
    private const string hello =
        "{\"v\":1,\"channel\":\"" + channel + "\",\"sid\":\"AAAAAAAAAAAAAAAAAAAAAA\",\"seq\":0,\"kind\":\"hello\","
        + "\"ciphertext\":\"eyJ0IjoiY29tbWl0In0\"}";
    private const string nullSidHello =
        "{\"v\":1,\"channel\":\"" + channel + "\",\"sid\":null,\"seq\":0,\"kind\":\"hello\","
        + "\"ciphertext\":\"eyJ0IjoiY29tbWl0In0\"}";

    private static readonly TimeSpan shortTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan connectTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan sendTimeout = TimeSpan.FromSeconds(30);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(HubAnswer.CloseAfterUpgrade)]
    [InlineData(HubAnswer.HandshakeError)]
    [InlineData(HubAnswer.SilentAfterUpgrade)]
    public async Task ShouldReportUnavailable_WhenHubFailsAfterUpgrade(HubAnswer answer) {
        await using FakeHub hub = FakeHub.Start(answer);
        HubConnector connector = new(hub.Address, new FakeTimeProvider(), shortTimeout);

        Task<IRelayConnection> connecting = connector.ConnectAsync(handle, Token);

        await Assert.ThrowsAsync<RelayUnavailableException>(() => connecting.WaitAsync(TestWaits.Step, Token));
    }

    [Fact]
    public async Task ShouldReportUnavailable_WhenHubNeverAnswersUpgrade() {
        await using FakeHub hub = FakeHub.Start(HubAnswer.NoAnswer);
        RecordingTime time = new();
        Task<IRelayConnection> connecting = new HubConnector(hub.Address, time).ConnectAsync(handle, Token);
        await time.WaitForAsync(connectTimeout);

        time.Advance(connectTimeout);

        await Assert.ThrowsAsync<RelayUnavailableException>(() => connecting.WaitAsync(TestWaits.Step, Token));
    }

    [Fact]
    public async Task ShouldReportUnavailable_WhenHubDropsDuringSend() {
        await using FakeHub hub = FakeHub.Open();
        await using IRelayConnection connection = await ConnectAsync(hub);
        Task sending = connection.SendAsync(EnvelopeFixtures.Hello(channel, EnvelopeFixtures.Sid), Token);
        await TestWaits.UntilAsync(() => hub.FramesRead > 0);

        hub.Drop();

        await Assert.ThrowsAsync<RelayUnavailableException>(() => sending.WaitAsync(TestWaits.Step, Token));
    }

    [Fact]
    public async Task ShouldReportUnavailable_WhenHubNeverAnswersSend() {
        await using FakeHub hub = FakeHub.Open();
        RecordingTime time = new();
        await using IRelayConnection connection = await new HubConnector(hub.Address, time).ConnectAsync(handle, Token);
        Task sending = connection.SendAsync(EnvelopeFixtures.Hello(channel, EnvelopeFixtures.Sid), Token);
        await time.WaitForAsync(sendTimeout);

        time.Advance(sendTimeout);

        await Assert.ThrowsAsync<RelayUnavailableException>(() => sending.WaitAsync(TestWaits.Step, Token));
    }

    [Theory]
    [InlineData(receiveMethod, nullSidHello)]
    [InlineData(sessionsClosedMethod, "{\"sids\":[null]}")]
    public async Task ShouldDropFrame_WhenRelaySendsInvalidFrame(string target, string argument) {
        await using FakeHub hub = FakeHub.Open(FakeHub.Invocation(target, argument), FakeHub.Invocation(receiveMethod, hello));

        await using IRelayConnection connection = await ConnectAsync(hub);

        Assert.Equal(new RelayEvent.Received(EnvelopeFixtures.Hello(channel, EnvelopeFixtures.Sid)), await NextAsync(connection));
    }

    [Fact]
    public async Task ShouldKeepValidSids_WhenNoticeHasInvalidSids() {
        await using FakeHub hub = FakeHub.Open(
            FakeHub.Invocation(sessionsClosedMethod, "{\"sids\":[\"AAAAAAAAAAAAAAAAAAAAAA\",null,\"short\"]}"));

        await using IRelayConnection connection = await ConnectAsync(hub);

        RelayEvent.SessionsClosed closed = Assert.IsType<RelayEvent.SessionsClosed>(await NextAsync(connection));
        Assert.Equal([EnvelopeFixtures.Sid], closed.Sids);
    }

    private static Task<IRelayConnection> ConnectAsync(FakeHub hub) =>
        new HubConnector(hub.Address, TimeProvider.System).ConnectAsync(handle, Token);

    private static async Task<RelayEvent> NextAsync(IRelayConnection connection) =>
        await connection.ReceiveAsync(Token).AsTask().WaitAsync(TestWaits.Step, Token);
}
