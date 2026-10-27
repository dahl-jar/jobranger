using System.Buffers.Text;
using System.Security.Cryptography;
using Jobranger.Contracts.Proofs;
using NSec.Cryptography;

namespace Jobranger.Contracts.Tests.Proofs;

public sealed class OwnershipProofTests {
    private const string cliVectorNonce = "AAECAwQFBgcICQoLDA0ODw";
    private const string cliVectorSignature =
        "VX8cfTvedl8Po_4YNqhOyEzpXHVLjQ7P48I1HjAMhmZGLk2mvhaLY6EellYLwfGJ8pxqHU6AC3T7tSDDqbg7DA";

    private readonly byte[] publicKey = Base64Url.DecodeFromChars(ProofVector.PublicKey);

    [Fact]
    public void ShouldSignLikeCliVector() {
        using Key signingKey = Key.Import(
            SignatureAlgorithm.Ed25519,
            SHA256.HashData("jobranger vector cli ed25519"u8),
            KeyBlobFormat.RawPrivateKey);

        byte[] signature = OwnershipProof.Sign(signingKey, OwnershipProof.PairStartPurpose, cliVectorNonce);

        Assert.Equal(cliVectorSignature, Base64Url.EncodeToString(signature));
    }

    [Theory]
    [InlineData(OwnershipProof.PairStartPurpose, ProofVector.PairStartSignature)]
    [InlineData(OwnershipProof.SessionCliPurpose, ProofVector.SessionCliSignature)]
    public void ShouldAcceptCliVectorSignature(string purpose, string signature) {
        bool valid = OwnershipProof.Verify(publicKey, purpose, ProofVector.Nonce, Base64Url.DecodeFromChars(signature));

        Assert.True(valid);
    }

    [Fact]
    public void ShouldRejectSignatureMadeForAnotherPurpose() {
        bool valid = OwnershipProof.Verify(
            publicKey,
            OwnershipProof.SessionCliPurpose,
            ProofVector.Nonce,
            Base64Url.DecodeFromChars(ProofVector.PairStartSignature));

        Assert.False(valid);
    }

    [Fact]
    public void ShouldRejectSignatureOverAnotherNonce() {
        bool valid = OwnershipProof.Verify(
            publicKey,
            OwnershipProof.PairStartPurpose,
            "nonce-two",
            Base64Url.DecodeFromChars(ProofVector.PairStartSignature));

        Assert.False(valid);
    }

    [Theory]
    [InlineData(31, 64)]
    [InlineData(32, 63)]
    public void ShouldRejectKeyOrSignatureOfWrongLength(int keyLength, int signatureLength) {
        byte[] signature = Base64Url.DecodeFromChars(ProofVector.PairStartSignature);

        bool valid = OwnershipProof.Verify(
            publicKey.AsSpan(0, keyLength), OwnershipProof.PairStartPurpose, ProofVector.Nonce, signature.AsSpan(0, signatureLength));

        Assert.False(valid);
    }
}
