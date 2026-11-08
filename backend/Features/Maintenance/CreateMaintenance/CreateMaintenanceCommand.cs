using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.CreateMaintenance;

public sealed record CreateMaintenanceCommand(
    long AssetId,
    long MaintenanceTypeId,
    // Técnico que lo realizó; sin valor, quien lo registra.
    long? PerformedBy,
    DateTime PerformedAt,
    DateTime? NextMaintenanceAt,
    string Description,
    string? PartsUsed,
    decimal? Cost,
    long? IncidentId) : ICommand<CreateMaintenanceResponse>;
