using Jobranger.Relay.Core;

namespace Jobranger.Relay.Pairing;

internal static class ProofCheck
{
    public static ProofResult Check(ProofRequest request, string purpose, NonceStore nonces)
    {
        if (request.Nonce is null
            || !CanonicalBase64Url.TryDecode(request.PublicKey, OwnershipProof.PublicKeyBytes, out byte[] publicKey)
            || !CanonicalBase64Url.TryDecode(request.Signature, OwnershipProof.SignatureBytes, out byte[] signature))
        {
            return Rejected(RelayProblems.InvalidRequest);
        }

        return nonces.Consume(request.Nonce) switch
        {
            NonceOutcome.Unknown => Rejected(RelayProblems.InvalidNonce),
            NonceOutcome.Expired => Rejected(RelayProblems.NonceExpired),
            _ when !OwnershipProof.Verify(publicKey, purpose, request.Nonce, signature) => Rejected(RelayProblems.InvalidSignature),
            _ => new ProofResult(ChannelId.FromPublicKey(publicKey), null),
        };
    }

    private static ProofResult Rejected(RelayProblem problem) => new(default, problem);
}
