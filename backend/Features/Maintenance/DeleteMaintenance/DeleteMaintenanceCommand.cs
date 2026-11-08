using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.DeleteMaintenance;

public sealed record DeleteMaintenanceCommand(long MaintenanceRecordId, int ExpectedVersion) : ICommand<DeleteMaintenanceResponse>;
