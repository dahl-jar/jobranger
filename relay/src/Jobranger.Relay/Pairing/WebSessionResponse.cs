namespace Jobranger.Relay.Pairing;

internal sealed record WebSessionResponse(string Channel, string Handle, DateTimeOffset HandleExpiresAt, string Ticket, DateTimeOffset TicketExpiresAt);
