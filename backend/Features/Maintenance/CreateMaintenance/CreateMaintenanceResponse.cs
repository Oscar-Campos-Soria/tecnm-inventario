namespace TecnmInventario.Api.Features.Maintenance.CreateMaintenance;

public sealed record CreateMaintenanceResponse(
    long MaintenanceRecordId,
    long AssetId,
    string MaintenanceTypeCode,
    DateTime PerformedAt,
    DateTime? NextMaintenanceAt,
    int Version,
    // Solo si el correctivo resolvió su incidente: estado final del activo.
    string? AssetStatusAfter = null);
