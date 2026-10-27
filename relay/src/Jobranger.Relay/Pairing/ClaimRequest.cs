namespace Jobranger.Relay.Pairing;

internal sealed record ClaimRequest(int? V, string? Code) : IVersionedRequest;
