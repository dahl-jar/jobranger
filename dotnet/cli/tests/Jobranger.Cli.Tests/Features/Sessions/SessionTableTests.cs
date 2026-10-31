using System.Text;
using Jobranger.Cli.Features.Sessions;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Messaging.Channels;
using Jobranger.Messaging.Envelopes;
using Microsoft.Extensions.Time.Testing;
using NSec.Cryptography;
using SessionsEntry = Jobranger.Cli.Features.Sessions.Sessions;

namespace Jobranger.Cli.Tests.Features.Sessions;

public sealed class SessionTableTests : IDisposable {
    private const string channelValue = "QzATLaCZ1ciBwANAgYReA3Np4WJ48r7ZPzlZsVwdxSQ";
    private const string frame = "{\"type\":\"ping\"}";
    private const string garbageStep = "bm90LWpzb24";

    private readonly Key cliStatic = Key.Create(KeyAgreementAlgorithm.X25519);
    private readonly FakeBrowserPeer peer = new();
    private readonly SessionsEntry sessions;

    public SessionTableTests() {
        sessions = new SessionsEntry(cliStatic, new ChannelId(channelValue), new FakeTimeProvider());
    }

    public void Dispose() {
        sessions.Dispose();
        peer.Dispose();
        cliStatic.Dispose();
    }

    [Fact]
    public void ShouldDeliverData_WhenPairedBrowserCompletesHandshake() {
        Complete(CommitRoute.Offer);
        (long seq, string ciphertext) = peer.Seal(frame);

        SessionUpdate update = sessions.Receive(Data(seq, ciphertext), Offer);

        SessionEvent.Data data = Assert.IsType<SessionEvent.Data>(Assert.Single(update.Events));
        Assert.Equal(frame, Encoding.UTF8.GetString(data.Plaintext));
    }

    [Fact]
    public void ShouldHoldConfirmedSession_WhenRouteSaysHold() {
        Complete(CommitRoute.Hold);
        (long seq, string ciphertext) = peer.Seal(frame);

        SessionUpdate update = sessions.Receive(Data(seq, ciphertext), Offer);

        Assert.Equal("abort", Steps(update));
    }

    [Theory]
    [InlineData("reveal")]
    [InlineData("data")]
    public void ShouldAnswerUnknownSession_WhenSidIsUnknown(string kind) {
        Envelope envelope = kind switch {
            "reveal" => Hello(peer.Reveal()),
            _ => Data(1, "AAAAAAAAAAAAAAAAAAAAAA"),
        };

        SessionUpdate update = sessions.Receive(envelope, Offer);

        Assert.Equal("unknown_session", Steps(update));
    }

    [Fact]
    public void ShouldStaySilent_WhenAbortArrivesForUnknownSid() {
        SessionUpdate update = sessions.Receive(Hello(FakeBrowserPeer.Encode("{\"t\":\"abort\"}")), Offer);

        Assert.Equal(SessionUpdate.Empty, update);
    }

    [Fact]
    public void ShouldCloseHandshake_WhenHelloSeqIsNotZero() {
        Offered(CommitRoute.Offer);

        SessionUpdate update = sessions.Receive(Hello(peer.Reveal()) with { Seq = 1 }, Offer);

        Assert.Equal(("abort", typeof(SessionEvent.Closed)), (Steps(update), Assert.Single(update.Events).GetType()));
    }

    [Fact]
    public void ShouldCloseHandshake_WhenStepIsGarbageOnKnownSid() {
        Offered(CommitRoute.Offer);

        SessionUpdate update = sessions.Receive(Hello(garbageStep), Offer);

        Assert.Equal(("abort", typeof(SessionEvent.Closed)), (Steps(update), Assert.Single(update.Events).GetType()));
    }

    [Fact]
    public void ShouldStaySilent_WhenStepIsGarbageOnUnknownSid() {
        SessionUpdate update = sessions.Receive(Hello(garbageStep), Offer);

        Assert.Equal(SessionUpdate.Empty, update);
    }

    [Fact]
    public void ShouldCloseHandshake_WhenRevealDoesNotMatchCommit() {
        Offered(CommitRoute.Offer);
        using Key swapped = Key.Create(KeyAgreementAlgorithm.X25519);
        peer.EphemeralPublic = swapped.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        SessionUpdate update = sessions.Receive(Hello(peer.Reveal()), Offer);

        Assert.Equal(("abort", typeof(SessionEvent.Closed)), (Steps(update), Assert.Single(update.Events).GetType()));
    }

    [Fact]
    public void ShouldCloseSession_WhenCommitArrivesOnKnownSid() {
        Complete(CommitRoute.Offer);

        SessionUpdate update = sessions.Receive(Hello(peer.Commit()), Offer);

        Assert.Equal("abort", Steps(update));
    }

    [Fact]
    public void ShouldCloseHandshake_WhenDataArrivesBeforeConfirm() {
        Offered(CommitRoute.Offer);
        _ = sessions.Receive(Hello(peer.Reveal()), Offer);

        SessionUpdate update = sessions.Receive(Data(1, "AAAAAAAAAAAAAAAAAAAAAA"), Offer);

        Assert.Equal("abort", Steps(update));
    }

    [Fact]
    public void ShouldCloseSession_WhenFrameSkipsSeq() {
        Complete(CommitRoute.Offer);
        _ = peer.Seal(frame);
        (long seq, string ciphertext) = peer.Seal(frame);

        SessionUpdate update = sessions.Receive(Data(seq, ciphertext), Offer);

        Assert.Equal(("abort", typeof(SessionEvent.Closed)), (Steps(update), Assert.Single(update.Events).GetType()));
    }

    [Fact]
    public void ShouldForgetSid_WhenBrowserAborts() {
        Offered(CommitRoute.Offer);
        SessionUpdate aborted = sessions.Receive(Hello(FakeBrowserPeer.Encode("{\"t\":\"abort\"}")), Offer);

        SessionUpdate update = sessions.Receive(Hello(peer.Reveal()), Offer);

        Assert.Equal(("", "unknown_session"), (Steps(aborted), Steps(update)));
    }

    [Fact]
    public void ShouldRefuseSeal_WhenConfirmedSessionIsHeld() {
        Complete(CommitRoute.Hold);

        Assert.Throws<InvalidOperationException>(() => sessions.Seal(peer.Sid, Encoding.UTF8.GetBytes(frame)));
    }

    [Fact]
    public void ShouldRefuseEstablish_WhenSessionWasOffered() {
        Complete(CommitRoute.Offer);

        Assert.Throws<InvalidOperationException>(() => sessions.Establish(peer.Sid));
    }

    [Theory]
    [InlineData("Firefox\u001b[31m on\u0085 \u202emacOS", "Firefox31m on macOS")]
    [InlineData("Chrome (Linux) 1.2/x_y-z", "Chrome (Linux) 1.2/x_y-z")]
    [InlineData("\u0007\u202e\u2028", "")]
    public void ShouldKeepOnlyAllowedCharacters_WhenAgentIsShown(string agent, string shown) {
        Assert.Equal(shown, SessionsEntry.CleanName(agent));
    }

    [Fact]
    public void ShouldCutAgentTo48Characters_WhenAgentIsLonger() {
        Assert.Equal(new string('a', 48), SessionsEntry.CleanName(new string('a', 49)));
    }

    private static CommitRoute Offer(HelloSteps.Commit commit) => CommitRoute.Offer;

    private static string Steps(SessionUpdate update) => string.Join(' ', update.Replies.Select(FakeRelay.Step));

    private void Offered(CommitRoute route) {
        SessionUpdate offered = sessions.Receive(Hello(peer.Commit()), _ => route);
        peer.ReadOffer(Assert.Single(offered.Replies).Ciphertext);
    }

    private void Complete(CommitRoute route) {
        Offered(route);
        _ = sessions.Receive(Hello(peer.Reveal()), Offer);
        _ = sessions.Receive(Hello(peer.Confirm()), Offer);
    }

    private Envelope Hello(string step) => new(1, channelValue, peer.Sid, 0, SessionsEntry.HelloKind, step);

    private Envelope Data(long seq, string ciphertext) => new(1, channelValue, peer.Sid, seq, SessionsEntry.DataKind, ciphertext);
}
