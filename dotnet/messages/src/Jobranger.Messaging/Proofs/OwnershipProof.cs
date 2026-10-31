using System.Text;
using NSec.Cryptography;

namespace Jobranger.Messaging.Proofs;

public static class OwnershipProof {
    public const string PairStartPurpose = "pair.start";
    public const string SessionCliPurpose = "session.cli";
    public const int PublicKeyBytes = 32;
    public const int SignatureBytes = 64;

    private const string prefix = "jobranger relay v1\n";

    private static readonly SignatureAlgorithm ed25519 = SignatureAlgorithm.Ed25519;

    public static byte[] Sign(Key signingKey, string purpose, string nonce) =>
        ed25519.Sign(signingKey, Message(purpose, nonce));

    public static bool Verify(ReadOnlySpan<byte> publicKey, string purpose, string nonce, ReadOnlySpan<byte> signature) =>
        PublicKey.TryImport(ed25519, publicKey, KeyBlobFormat.RawPublicKey, out PublicKey? key)
        && key is not null
        && ed25519.Verify(key, Message(purpose, nonce), signature);

    private static byte[] Message(string purpose, string nonce) => Encoding.UTF8.GetBytes($"{prefix}{purpose}\n{nonce}");
}
