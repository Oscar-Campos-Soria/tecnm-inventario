namespace TecnmInventario.Api.Features.Tickets.UpdateTicketStatus;

public sealed record UpdateTicketStatusResponse(
    long TicketId,
    string PreviousStatus,
    string Status,
    int Version);
