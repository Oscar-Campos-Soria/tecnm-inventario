namespace TecnmInventario.Api.Features.Tickets.GetTickets;

public sealed record GetTicketsResponse(
    long TicketId,
    string Title,
    string CategoryCode,
    string CategoryName,
    string PriorityCode,
    string PriorityName,
    string Status,
    string RequesterUsername,
    string RequesterName,
    string? AssignedToUsername,
    string? AssetInventoryCode,
    long? IncidentId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version);
