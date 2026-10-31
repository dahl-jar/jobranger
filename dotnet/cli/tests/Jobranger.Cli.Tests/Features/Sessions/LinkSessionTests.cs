using System.Text;
using Jobranger.Cli.Features.Sessions;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Sessions;

public sealed class LinkSessionTests : IDisposable {
    private const string frame = "{\"type\":\"run.start\"}";
    private const long lastSeqBelowLimit = uint.MaxValue;
    private const int overMaxChars = 61444;
    private const int maxPlaintextBytes = 46064;
    private const int belowTagChars = 20;
    private const string looseCiphertext = "AAAAAAAAAAAAAAAAAAAAAAB";
    private const int sealThreads = 8;
    private const int framesPerThread = 1000;

    private readonly FakeBrowserPeer peer = new();
    private readonly LinkSession session;

    public LinkSessionTests() {
        session = Handshakes.Complete(peer);
    }

    public static TheoryData<string> BadSizes => [new string('A', overMaxChars), new string('A', belowTagChars), looseCiphertext];

    public void Dispose() {
        session.Dispose();
        peer.Dispose();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ShouldRejectFrame_WhenSeqIsNotLastPlusOne(long seq) {
        (_, string ciphertext) = peer.Seal(frame);

        FrameFailure reason = Failure(() => session.Open(seq, ciphertext));

        Assert.Equal(FrameFailure.Seq, reason);
    }

    [Fact]
    public void ShouldRejectFrame_WhenSeqRepeats() {
        (long seq, string ciphertext) = peer.Seal(frame);
        _ = session.Open(seq, ciphertext);

        FrameFailure reason = Failure(() => session.Open(seq, ciphertext));

        Assert.Equal(FrameFailure.Seq, reason);
    }

    [Fact]
    public void ShouldRejectFrame_WhenSeqReachesTwoToThe32() {
        using LinkSession nearLimit = VectorSession(lastReceived: lastSeqBelowLimit);
        (_, string ciphertext) = peer.Seal(frame);

        FrameFailure reason = Failure(() => nearLimit.Open(lastSeqBelowLimit + 1, ciphertext));

        Assert.Equal(FrameFailure.Seq, reason);
    }

    [Fact]
    public void ShouldCloseSession_WhenNextSentSeqWouldReachTwoToThe32() {
        using LinkSession nearLimit = VectorSession(lastSent: lastSeqBelowLimit);
        byte[] plaintext = Encoding.UTF8.GetBytes(frame);

        FrameFailure[] reasons = [Failure(() => nearLimit.Seal(plaintext)), Failure(() => nearLimit.Seal(plaintext))];

        Assert.Equal([FrameFailure.Seq, FrameFailure.Closed], reasons);
    }

    [Fact]
    public async Task ShouldSealDistinctSeqs_WhenThreadsShareSession() {
        byte[] plaintext = Encoding.UTF8.GetBytes(frame);
        using Barrier start = new(sealThreads);

        (long Seq, string Ciphertext)[][] sealedFrames = await Task.WhenAll(Enumerable.Range(0, sealThreads)
            .Select(_ => Task.Factory.StartNew(() => {
                start.SignalAndWait(TestContext.Current.CancellationToken);
                return Enumerable.Range(0, framesPerThread).Select(_ => session.Seal(plaintext)).ToArray();
            }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default)));
        (long Seq, string Ciphertext)[] frames = [.. sealedFrames.SelectMany(batch => batch)];

        Assert.Equal(sealThreads * framesPerThread, frames.Select(sealedFrame => sealedFrame.Seq).Distinct().Count());
        Assert.All(frames, sealedFrame => Assert.Equal(frame, peer.Open(sealedFrame.Seq, sealedFrame.Ciphertext)));
    }

    [Theory]
    [MemberData(nameof(BadSizes))]
    public void ShouldRejectFrame_WhenCiphertextSizeIsWrong(string ciphertext) {
        FrameFailure reason = Failure(() => session.Open(1, ciphertext));

        Assert.Equal(FrameFailure.Size, reason);
    }

    [Fact]
    public void ShouldOpenFrame_WhenCiphertextIsExactly61440Chars() {
        string plaintext = new('a', maxPlaintextBytes);
        (long seq, string ciphertext) = peer.Seal(plaintext);

        byte[] opened = session.Open(seq, ciphertext);

        Assert.Equal(plaintext, Encoding.UTF8.GetString(opened));
    }

    [Fact]
    public void ShouldRejectFrame_WhenTagFails() {
        (long seq, string ciphertext) = peer.Seal(frame);
        string tampered = (ciphertext[0] == 'A' ? "B" : "A") + ciphertext[1..];

        FrameFailure reason = Failure(() => session.Open(seq, tampered));

        Assert.Equal(FrameFailure.Tag, reason);
    }

    [Fact]
    public void ShouldRefuseFrames_WhenEarlierFrameBrokeRules() {
        (long seq, string ciphertext) = peer.Seal(frame);
        _ = Failure(() => session.Open(seq + 1, ciphertext));

        FrameFailure reason = Failure(() => session.Open(seq, ciphertext));

        Assert.Equal(FrameFailure.Closed, reason);
    }

    [Fact]
    public void ShouldRefuseSeal_WhenEarlierFrameBrokeRules() {
        (long seq, string ciphertext) = peer.Seal(frame);
        _ = Failure(() => session.Open(seq + 1, ciphertext));

        FrameFailure reason = Failure(() => session.Seal(Encoding.UTF8.GetBytes(frame)));

        Assert.Equal(FrameFailure.Closed, reason);
    }

    [Fact]
    public void ShouldRefuseFrames_WhenSessionIsDisposed() {
        (long seq, string ciphertext) = peer.Seal(frame);
        session.Dispose();

        FrameFailure[] reasons = [Failure(() => session.Seal(Encoding.UTF8.GetBytes(frame))), Failure(() => session.Open(seq, ciphertext))];

        Assert.Equal([FrameFailure.Closed, FrameFailure.Closed], reasons);
    }

    private static LinkSession VectorSession(long lastSent = 0, long lastReceived = 0) => new(
        SessionVector.ShortValue(SessionVector.SidLabel),
        SessionVector.Bytes("key_c2b"),
        SessionVector.Bytes("key_b2c"),
        lastSent,
        lastReceived);

    private static FrameFailure Failure(Func<object> act) => Assert.Throws<FrameException>(act).Reason;
}
