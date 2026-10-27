namespace Jobranger.Relay.Endpoints;

internal sealed record TicketRequest(int? V, string? Ticket) : IVersionedRequest;
