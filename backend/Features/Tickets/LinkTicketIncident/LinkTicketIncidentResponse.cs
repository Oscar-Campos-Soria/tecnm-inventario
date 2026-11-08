namespace TecnmInventario.Api.Features.Tickets.LinkTicketIncident;

public sealed record LinkTicketIncidentResponse(
    long TicketId,
    long IncidentId,
    long AssetId,
    int Version);
