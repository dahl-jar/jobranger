using System.Buffers.Text;
using System.Text;
using NSec.Cryptography;

namespace Jobranger.Relay.Core.Tests;

public sealed class OwnershipProofTests {
    private static readonly SignatureAlgorithm ed25519 = SignatureAlgorithm.Ed25519;

    private readonly byte[] publicKey = Base64Url.DecodeFromChars(ProofVector.PublicKey);

    [Theory]
    [InlineData(OwnershipProof.PairStartPurpose, ProofVector.PairStartSignature)]
    [InlineData(OwnershipProof.SessionCliPurpose, ProofVector.SessionCliSignature)]
    public void ShouldAcceptCliVectorSignature(string purpose, string signature) {
        bool valid = OwnershipProof.Verify(publicKey, purpose, ProofVector.Nonce, Base64Url.DecodeFromChars(signature));

        Assert.True(valid);
    }

    [Fact]
    public void ShouldAcceptSignatureOverPurposeAndNonce() {
        using Key key = Key.Create(ed25519);
        byte[] signature = ed25519.Sign(key, Encoding.UTF8.GetBytes("jobranger relay v1\npair.start\nnonce-one"));

        bool valid = OwnershipProof.Verify(
            key.PublicKey.Export(KeyBlobFormat.RawPublicKey), OwnershipProof.PairStartPurpose, "nonce-one", signature);

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
