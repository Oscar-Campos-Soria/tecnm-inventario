namespace TecnmInventario.Api.Features.Tickets.ResolveTicket;

public sealed record ResolveTicketResponse(
    long TicketId,
    string Status,
    DateTime ResolvedAt,
    DateTime AutoCloseAt,
    int Version);
