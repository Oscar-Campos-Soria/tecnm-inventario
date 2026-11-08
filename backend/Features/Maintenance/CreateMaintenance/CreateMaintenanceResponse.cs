namespace TecnmInventario.Api.Features.Maintenance.CreateMaintenance;

public sealed record CreateMaintenanceResponse(
    long MaintenanceRecordId,
    long AssetId,
    string MaintenanceTypeCode,
    DateTime PerformedAt,
    DateTime? NextMaintenanceAt,
    int Version);
