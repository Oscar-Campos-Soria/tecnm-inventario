namespace TecnmInventario.Api.Features.Maintenance.GetMaintenance;

// Fila de historial; también la usa GetUpcomingMaintenance.
public sealed record MaintenanceItem(
    long MaintenanceRecordId,
    long AssetId,
    string AssetInventoryCode,
    string TypeCode,
    string TypeName,
    string? PerformedByName,
    DateTime? PerformedAt,
    DateTime? NextMaintenanceAt,
    string? Description,
    string? PartsUsed,
    decimal? Cost,
    long? IncidentId,
    int Version,
    // Solo en mantenimientos programados todavía sin realizar.
    DateTime? ScheduledAt = null);
