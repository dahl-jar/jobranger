namespace Jobranger.Relay.Endpoints;

internal sealed record ClaimRequest(int? V, string? Code) : IVersionedRequest;
