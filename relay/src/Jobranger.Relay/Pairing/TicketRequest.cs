namespace Jobranger.Relay.Pairing;

internal sealed record TicketRequest(int? V, string? Ticket) : IVersionedRequest;
