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
    long? IncidentId,
    // Correctivo ligado: resolver también el incidente, en la misma transacción.
    bool ResolveIncident = false,
    // Solución que queda en el incidente; sin valor, la descripción del correctivo.
    string? IncidentSolution = null) : ICommand<CreateMaintenanceResponse>, Common.Behaviors.ITransactionalCommand;
