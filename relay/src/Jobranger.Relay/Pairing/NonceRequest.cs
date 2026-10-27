namespace Jobranger.Relay.Pairing;

internal sealed record NonceRequest(int? V, string? Channel) : IVersionedRequest;
