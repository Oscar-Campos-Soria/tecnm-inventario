namespace TecnmInventario.Api.Features.Tickets.UpdateTicketPriority;

public sealed record UpdateTicketPriorityResponse(
    long TicketId,
    string PriorityCode,
    int Version);
