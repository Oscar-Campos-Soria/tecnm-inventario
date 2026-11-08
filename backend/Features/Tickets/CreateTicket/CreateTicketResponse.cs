namespace TecnmInventario.Api.Features.Tickets.CreateTicket;

public sealed record CreateTicketResponse(
    long TicketId,
    string Status,
    string PriorityCode,
    DateTime CreatedAt,
    int Version);
