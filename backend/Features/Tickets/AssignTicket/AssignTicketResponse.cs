namespace TecnmInventario.Api.Features.Tickets.AssignTicket;

public sealed record AssignTicketResponse(
    long TicketId,
    long AssignedToId,
    string AssignedToUsername,
    string Status,
    int Version);
