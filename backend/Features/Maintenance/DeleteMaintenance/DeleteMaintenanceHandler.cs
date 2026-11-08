using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Maintenance.DeleteMaintenance;

/// <summary>Baja lógica de un registro capturado por error (auditada).</summary>
public sealed class DeleteMaintenanceHandler : ICommandHandler<DeleteMaintenanceCommand, DeleteMaintenanceResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteMaintenanceHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteMaintenanceResponse> Handle(
        DeleteMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var record = await _context.MaintenanceRecords
            .FirstOrDefaultAsync(r => r.MaintenanceRecordId == command.MaintenanceRecordId && !r.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El registro de mantenimiento no existe.");

        await _softDeleteAuditor.DeleteAsync(
            record,
            recordId: record.MaintenanceRecordId,
            expectedVersion: command.ExpectedVersion,
            tableName: "maintenance_records",
            snapshotBeforeDelete: new
            {
                record.MaintenanceRecordId,
                record.AssetId,
                record.MaintenanceTypeId,
                record.PerformedBy,
                record.PerformedAt,
                record.NextMaintenanceAt,
                record.Description,
                record.Cost,
                record.IncidentId,
                record.Version
            },
            cancellationToken);

        return new DeleteMaintenanceResponse(record.MaintenanceRecordId, true);
    }
}
