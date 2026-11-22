namespace TecnmInventario.Api.Features.Tickets.GetTicketStats;

public sealed record CodeCount(string Code, int Count);

/// <summary>
/// Created: creados en el lapso. Resolved/Closed: con resolved_at/closed_at en el lapso (pudieron crearse antes).
/// Cancelled: cancelados con última actualización en el lapso (no se guarda la fecha de cancelación).
/// AvgResolutionHours: de los resueltos en el lapso. OpenNow: pendientes hoy, sin importar cuándo se crearon.
/// </summary>
public sealed record GetTicketStatsResponse(
    DateTime From,
    DateTime To,
    int Created,
    int Resolved,
    int Closed,
    int Cancelled,
    int CreatedStillOpen,
    double? AvgResolutionHours,
    int OpenNow,
    IReadOnlyList<CodeCount> CreatedByPriority,
    IReadOnlyList<CodeCount> CreatedByCategory);
