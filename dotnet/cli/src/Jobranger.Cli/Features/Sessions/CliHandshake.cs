using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;

namespace Jobranger.Cli.Features.Sessions;

internal sealed class CliHandshake : IDisposable {
    private const int sharedSecrets = 3;
    private const int thirdSecretStart = SessionSecrets.KeyBytes * 2;

    private readonly Key cliStatic;
    private readonly Key cliEph;
    private readonly byte[] sid;
    private readonly HelloSteps.Commit commit;
    private SessionSecrets? secrets;
    private HandshakeState state = HandshakeState.Committed;

    internal CliHandshake(Key cliStatic, ReadOnlySpan<byte> sid, HelloSteps.Commit commit, Key cliEph) {
        this.cliStatic = cliStatic;
        this.cliEph = cliEph;
        this.sid = sid.ToArray();
        this.commit = commit;
    }

    private enum HandshakeState {
        Committed,
        Offered,
        Revealed,
        Done,
        Failed,
    }

    public string Agent { get; private set; } = "";

    public IReadOnlyList<string> Words { get; private set; } = [];

    public HelloSteps.Offer Offer() {
        Expect(HandshakeState.Committed);
        state = HandshakeState.Offered;
        return new HelloSteps.Offer(PublicBytes(cliStatic), PublicBytes(cliEph));
    }

    public HelloSteps.Confirm OnReveal(HelloSteps.Reveal reveal) {
        Expect(HandshakeState.Offered);
        byte[] agent = Encoding.UTF8.GetBytes(reveal.Agent);
        byte[] recomputed = SessionSecrets.Commit(commit.BrowserStatic, reveal.BrowserEph, reveal.NonceB, agent);
        if (!CryptographicOperations.FixedTimeEquals(recomputed, commit.Hash)) {
            throw Fail(HandshakeFailure.Commit);
        }
        byte[] ikm = new byte[SessionSecrets.KeyBytes * sharedSecrets];
        SessionSecrets derived;
        try {
            if (!Agree(reveal.BrowserEph, commit.BrowserStatic, ikm)) {
                throw Fail(HandshakeFailure.Zero);
            }
            byte[] transcript = SessionSecrets.Transcript(
                sid,
                PublicBytes(cliStatic),
                commit.BrowserStatic,
                PublicBytes(cliEph),
                reveal.BrowserEph,
                reveal.NonceB,
                agent);
            derived = SessionSecrets.Derive(ikm, transcript);
        } finally {
            CryptographicOperations.ZeroMemory(ikm);
            cliEph.Dispose();
        }
        secrets = derived;
        Agent = reveal.Agent;
        Words = SasWords.From(derived.Sas, SasWords.List);
        state = HandshakeState.Revealed;
        return new HelloSteps.Confirm(derived.CliMac());
    }

    public LinkSession OnConfirm(HelloSteps.Confirm confirm) {
        Expect(HandshakeState.Revealed);
        SessionSecrets revealed = secrets!;
        if (!CryptographicOperations.FixedTimeEquals(confirm.Mac, revealed.BrowserMac())) {
            throw Fail(HandshakeFailure.Mac);
        }
        LinkSession session = new(sid, revealed.KeyC2b, revealed.KeyB2c);
        state = HandshakeState.Done;
        DropSecrets();
        return session;
    }

    public void Dispose() {
        cliEph.Dispose();
        DropSecrets();
    }

    private static byte[] PublicBytes(Key key) => key.PublicKey.Export(KeyBlobFormat.RawPublicKey);

    private bool Agree(byte[] browserEph, byte[] browserStatic, Span<byte> ikm) =>
        SessionSecrets.TryAgree(cliEph, browserEph, ikm[..SessionSecrets.KeyBytes])
        && SessionSecrets.TryAgree(cliStatic, browserEph, ikm.Slice(SessionSecrets.KeyBytes, SessionSecrets.KeyBytes))
        && SessionSecrets.TryAgree(cliEph, browserStatic, ikm[thirdSecretStart..]);

    private void Expect(HandshakeState expected) {
        if (state != expected) {
            throw Fail(HandshakeFailure.Step);
        }
    }

    private HandshakeException Fail(HandshakeFailure reason) {
        state = HandshakeState.Failed;
        cliEph.Dispose();
        DropSecrets();
        return new HandshakeException(reason);
    }

    private void DropSecrets() {
        secrets?.Dispose();
        secrets = null;
    }
}
