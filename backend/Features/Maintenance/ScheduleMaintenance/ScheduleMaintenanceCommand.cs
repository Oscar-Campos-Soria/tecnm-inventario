using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;

/// <param name="PerformedBy">Técnico asignado (opcional).</param>
public sealed record ScheduleMaintenanceCommand(
    long AssetId, long MaintenanceTypeId, DateTime ScheduledAt, string Description, long? PerformedBy)
    : ICommand<ScheduleMaintenanceResponse>, ITransactionalCommand;

public sealed record ScheduleMaintenanceResponse(long MaintenanceRecordId, DateTime ScheduledAt, int Version);
