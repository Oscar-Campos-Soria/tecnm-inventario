using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;

/// <param name="Description">Qué se hizo; sin valor se conserva lo programado.</param>
/// <param name="PerformedBy">Quién lo hizo; sin valor, el asignado o quien registra.</param>
public sealed record CompleteMaintenanceCommand(
    long MaintenanceRecordId, int ExpectedVersion, DateTime PerformedAt, string? Description,
    string? PartsUsed, decimal? Cost, DateTime? NextMaintenanceAt, long? PerformedBy)
    : ICommand<CompleteMaintenanceResponse>, ITransactionalCommand;

public sealed record CompleteMaintenanceResponse(long MaintenanceRecordId, DateTime PerformedAt, int Version);
