namespace Jobranger.Relay.Endpoints;

internal sealed record WebSessionResponse(string Channel, string Handle, DateTimeOffset HandleExpiresAt, string Ticket, DateTimeOffset TicketExpiresAt);
