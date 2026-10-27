namespace Jobranger.Relay.Endpoints;

internal sealed record NonceRequest(int? V, string? Channel) : IVersionedRequest;
