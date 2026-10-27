using System.Text;
using Jobranger.Cli.Features.Sessions;
using NSec.Cryptography;

namespace Jobranger.Cli.Tests.Features.Sessions;

public sealed class SessionSecretsTests {
    private const int keyBytes = 32;
    private const int sharedSecrets = 3;
    private const int thirdSecretStart = keyBytes * 2;

    [Fact]
    public void ShouldReproduceVector_WhenInputsComeFromSessionV1() {
        using Key cliStatic = SessionVector.PrivateKey(SessionVector.CliStaticLabel);
        using Key cliEph = SessionVector.PrivateKey(SessionVector.CliEphemeralLabel);
        byte[] browserStatic = SessionVector.Bytes("browser_static_public");
        byte[] browserEph = SessionVector.Bytes("browser_eph_public");
        byte[] nonceB = SessionVector.ShortValue(SessionVector.NonceLabel);
        byte[] agent = Encoding.UTF8.GetBytes(SessionVector.Text("agent"));
        byte[] ikm = new byte[keyBytes * sharedSecrets];
        _ = SessionSecrets.TryAgree(cliEph, browserEph, ikm.AsSpan(0, keyBytes));
        _ = SessionSecrets.TryAgree(cliStatic, browserEph, ikm.AsSpan(keyBytes, keyBytes));
        _ = SessionSecrets.TryAgree(cliEph, browserStatic, ikm.AsSpan(thirdSecretStart, keyBytes));
        byte[] transcript = SessionSecrets.Transcript(
            SessionVector.ShortValue(SessionVector.SidLabel),
            cliStatic.PublicKey.Export(KeyBlobFormat.RawPublicKey),
            browserStatic,
            cliEph.PublicKey.Export(KeyBlobFormat.RawPublicKey),
            browserEph,
            nonceB,
            agent);

        using SessionSecrets secrets = SessionSecrets.Derive(ikm, transcript);

        Assert.Equal(SessionVector.Bytes("commit"), SessionSecrets.Commit(browserStatic, browserEph, nonceB, agent));
        Assert.Equal(SessionVector.Bytes("ikm"), ikm);
        Assert.Equal(SessionVector.Bytes("transcript_hash"), secrets.TranscriptHash);
        Assert.Equal(SessionVector.Bytes("key_c2b"), secrets.KeyC2b);
        Assert.Equal(SessionVector.Bytes("key_b2c"), secrets.KeyB2c);
        Assert.Equal(SessionVector.Bytes("key_confirm"), secrets.KeyConfirm);
        Assert.Equal(SessionVector.Bytes("sas"), secrets.Sas);
        Assert.Equal(SessionVector.Bytes("confirm_cli"), secrets.CliMac());
        Assert.Equal(SessionVector.Bytes("confirm_browser"), secrets.BrowserMac());
    }

    [Fact]
    public void ShouldZeroEveryKey_WhenDisposed() {
        SessionSecrets secrets = SessionSecrets.Derive(new byte[keyBytes * sharedSecrets], SessionVector.Bytes("transcript_hash"));
        byte[][] held = [secrets.TranscriptHash, secrets.KeyC2b, secrets.KeyB2c, secrets.KeyConfirm, secrets.Sas];

        secrets.Dispose();

        Assert.All(held, bytes => Assert.All(bytes, value => Assert.Equal(0, value)));
    }
}
