namespace Jobranger.Relay.Pairing;

internal sealed record ProofRequest(int? V, string? PublicKey, string? Nonce, string? Signature) : IVersionedRequest;
