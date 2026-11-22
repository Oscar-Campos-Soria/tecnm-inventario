namespace TecnmInventario.Api.Features.Tickets.GetTicketWorkload;

/// <summary>OldestOpenAt: el ticket abierto más antiguo (antigüedad, no atraso: el sistema no tiene fechas límite).</summary>
public sealed record AssigneeWorkload(
    string Username,
    string FullName,
    int OpenTickets,
    int CriticalOpenTickets,
    int OpenIncidents,
    DateTime? OldestOpenTicketAt);

public sealed record GetTicketWorkloadResponse(
    IReadOnlyList<AssigneeWorkload> Assignees,
    int UnassignedOpenTickets,
    int UnassignedOpenIncidents);
