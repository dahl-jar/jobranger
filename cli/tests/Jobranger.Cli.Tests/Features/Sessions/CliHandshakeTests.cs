using System.Buffers.Text;
using System.Text;
using Jobranger.Cli.Features.Sessions;
using Jobranger.Cli.Tests.Mocks;
using NSec.Cryptography;
using SessionsEntry = Jobranger.Cli.Features.Sessions.Sessions;

namespace Jobranger.Cli.Tests.Features.Sessions;

public sealed class CliHandshakeTests : IDisposable {
    private const int keyBytes = 32;
    private const string firstFrame = "{\"type\":\"run.start\"}";
    private const string secondFrame = "{\"type\":\"run.cancel\"}";
    private const string reply = "{\"type\":\"saved\"}";

    private readonly Key cliStatic = Key.Create(KeyAgreementAlgorithm.X25519);

    public void Dispose() => cliStatic.Dispose();

    [Fact]
    public void ShouldReproduceVector_WhenInputsComeFromSessionV1() {
        using Key vectorStatic = SessionVector.PrivateKey(SessionVector.CliStaticLabel);
        using FakeBrowserPeer peer = SessionVector.Peer();
        using CliHandshake handshake = new(
            vectorStatic,
            SessionVector.ShortValue(SessionVector.SidLabel),
            Handshakes.Parse<HelloSteps.Commit>(peer.Commit()),
            SessionVector.PrivateKey(SessionVector.CliEphemeralLabel));

        HelloSteps.Offer offer = handshake.Offer();
        peer.ReadOffer(HelloSteps.Encode(offer));
        HelloSteps.Confirm confirm = handshake.OnReveal(Handshakes.Parse<HelloSteps.Reveal>(peer.Reveal()));
        using LinkSession session = handshake.OnConfirm(Handshakes.Parse<HelloSteps.Confirm>(peer.Confirm()));
        (long seq, string ciphertext) = session.Seal(Encoding.UTF8.GetBytes(SessionVector.Text("frame_plaintext")));

        Assert.Equal(SessionVector.Bytes("cli_static_public"), offer.CliStatic);
        Assert.Equal(SessionVector.Bytes("cli_eph_public"), offer.CliEph);
        Assert.Equal(SessionVector.Bytes("confirm_cli"), confirm.Mac);
        Assert.Equal(SessionVector.Words(), handshake.Words);
        Assert.Equal((SessionVector.Number("frame_seq"), SessionVector.Text("frame_ciphertext")), (seq, ciphertext));
    }

    [Fact]
    public void ShouldExchangeFrames_WhenBrowserPeerCompletesHandshake() {
        using FakeBrowserPeer peer = new();
        using LinkSession session = Handshakes.Complete(peer);
        (long firstSeq, string first) = peer.Seal(firstFrame);
        (long secondSeq, string second) = peer.Seal(secondFrame);

        string[] opened = [
            Encoding.UTF8.GetString(session.Open(firstSeq, first)),
            Encoding.UTF8.GetString(session.Open(secondSeq, second)),
        ];
        (long replySeq, string sealedReply) = session.Seal(Encoding.UTF8.GetBytes(reply));

        Assert.Equal([firstFrame, secondFrame], opened);
        Assert.Equal(reply, peer.Open(replySeq, sealedReply));
    }

    [Fact]
    public void ShouldShowBrowserWords_WhenRevealMatchesCommit() {
        using FakeBrowserPeer peer = new();
        using CliHandshake handshake = Handshakes.Accept(cliStatic, peer);
        peer.ReadOffer(HelloSteps.Encode(handshake.Offer()));

        HelloSteps.Confirm confirm = handshake.OnReveal(Handshakes.Parse<HelloSteps.Reveal>(peer.Reveal()));

        Assert.True(peer.AcceptsCliConfirm(HelloSteps.Encode(confirm)));
        Assert.Equal(peer.Words, handshake.Words);
        Assert.Equal(peer.Agent, handshake.Agent);
    }

    [Fact]
    public void ShouldAbort_WhenRevealDiffersFromCommit() {
        using FakeBrowserPeer peer = new();
        using CliHandshake handshake = Handshakes.Offered(cliStatic, peer);
        HelloSteps.Reveal other = Handshakes.Parse<HelloSteps.Reveal>(peer.Reveal("Chrome on Linux"));

        HandshakeFailure reason = Failure(() => handshake.OnReveal(other));

        Assert.Equal(HandshakeFailure.Commit, reason);
    }

    [Fact]
    public void ShouldDisposeEphemeralKey_WhenRevealDiffersFromCommit() {
        using FakeBrowserPeer peer = new();
        Key cliEph = Key.Create(KeyAgreementAlgorithm.X25519);
        using CliHandshake handshake = new(
            cliStatic,
            Base64Url.DecodeFromChars(peer.Sid),
            Handshakes.Parse<HelloSteps.Commit>(peer.Commit()),
            cliEph);
        peer.ReadOffer(HelloSteps.Encode(handshake.Offer()));
        _ = Failure(() => handshake.OnReveal(Handshakes.Parse<HelloSteps.Reveal>(peer.Reveal("Chrome on Linux"))));

        Assert.Throws<ObjectDisposedException>(() => SessionSecrets.TryAgree(cliEph, peer.StaticPublic, new byte[keyBytes]));
    }

    [Fact]
    public void ShouldAbort_WhenSharedSecretIsAllZero() {
        using FakeBrowserPeer peer = new() { EphemeralPublic = new byte[keyBytes] };
        using CliHandshake handshake = Handshakes.Offered(cliStatic, peer);

        HandshakeFailure reason = Failure(() => handshake.OnReveal(Handshakes.Parse<HelloSteps.Reveal>(peer.Reveal())));

        Assert.Equal(HandshakeFailure.Zero, reason);
    }

    [Fact]
    public void ShouldAbort_WhenBrowserMacIsBad() {
        using FakeBrowserPeer peer = new();
        using CliHandshake handshake = Revealed(peer);
        HelloSteps.Confirm bad = Handshakes.Parse<HelloSteps.Confirm>(peer.Confirm(new byte[keyBytes]));

        HandshakeFailure reason = Failure(() => handshake.OnConfirm(bad));

        Assert.Equal(HandshakeFailure.Mac, reason);
    }

    [Fact]
    public void ShouldAbort_WhenRevealComesBeforeOffer() {
        using FakeBrowserPeer peer = new();
        using CliHandshake handshake = Handshakes.Accept(cliStatic, peer);

        HandshakeFailure reason = Failure(() => handshake.OnReveal(Handshakes.Parse<HelloSteps.Reveal>(peer.Reveal())));

        Assert.Equal(HandshakeFailure.Step, reason);
    }

    [Fact]
    public void ShouldAbort_WhenConfirmComesBeforeReveal() {
        using FakeBrowserPeer peer = new();
        using CliHandshake handshake = Handshakes.Offered(cliStatic, peer);

        HandshakeFailure reason = Failure(() => handshake.OnConfirm(new HelloSteps.Confirm(new byte[keyBytes])));

        Assert.Equal(HandshakeFailure.Step, reason);
    }

    [Fact]
    public void ShouldRefuseConfirm_WhenHandshakeAlreadyFailed() {
        using FakeBrowserPeer peer = new();
        using CliHandshake handshake = Revealed(peer);
        _ = Failure(() => handshake.OnConfirm(Handshakes.Parse<HelloSteps.Confirm>(peer.Confirm(new byte[keyBytes]))));

        HandshakeFailure reason = Failure(() => handshake.OnConfirm(Handshakes.Parse<HelloSteps.Confirm>(peer.Confirm())));

        Assert.Equal(HandshakeFailure.Step, reason);
    }

    [Fact]
    public void ShouldRefuseReveal_WhenEarlierRevealFailed() {
        using FakeBrowserPeer peer = new();
        using CliHandshake handshake = Handshakes.Offered(cliStatic, peer);
        _ = Failure(() => handshake.OnReveal(Handshakes.Parse<HelloSteps.Reveal>(peer.Reveal("Chrome on Linux"))));

        HandshakeFailure reason = Failure(() => handshake.OnReveal(Handshakes.Parse<HelloSteps.Reveal>(peer.Reveal())));

        Assert.Equal(HandshakeFailure.Step, reason);
    }

    [Fact]
    public void ShouldAbort_WhenOfferIsAskedTwice() {
        using FakeBrowserPeer peer = new();
        using CliHandshake handshake = Handshakes.Offered(cliStatic, peer);

        HandshakeFailure reason = Failure(() => handshake.Offer());

        Assert.Equal(HandshakeFailure.Step, reason);
    }

    [Fact]
    public void ShouldAbort_WhenSidIsNotSixteenBytes() {
        using FakeBrowserPeer peer = new();
        HelloSteps.Commit commit = Handshakes.Parse<HelloSteps.Commit>(peer.Commit());
        string longSid = Base64Url.EncodeToString(new byte[keyBytes]);

        HandshakeFailure reason = Failure(() => SessionsEntry.Accept(cliStatic, longSid, commit));

        Assert.Equal(HandshakeFailure.Step, reason);
    }

    private CliHandshake Revealed(FakeBrowserPeer peer) {
        CliHandshake handshake = Handshakes.Offered(cliStatic, peer);
        _ = handshake.OnReveal(Handshakes.Parse<HelloSteps.Reveal>(peer.Reveal()));
        return handshake;
    }

    private static HandshakeFailure Failure(Func<object> step) => Assert.Throws<HandshakeException>(step).Reason;
}
