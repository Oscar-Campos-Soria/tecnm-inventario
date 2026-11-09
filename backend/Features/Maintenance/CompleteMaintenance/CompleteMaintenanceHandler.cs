using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;

/// <summary>Un mantenimiento programado se marca como realizado y pasa al historial.</summary>
public sealed class CompleteMaintenanceHandler : ICommandHandler<CompleteMaintenanceCommand, CompleteMaintenanceResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CompleteMaintenanceHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CompleteMaintenanceResponse> Handle(
        CompleteMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var record = await _context.MaintenanceRecords
            .FirstOrDefaultAsync(r => r.MaintenanceRecordId == command.MaintenanceRecordId && !r.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El mantenimiento solicitado no existe.");

        if (record.Version != command.ExpectedVersion)
            throw new DbUpdateConcurrencyException("El mantenimiento fue modificado por otra persona.");
        if (record.PerformedAt is not null)
            throw new BusinessRuleViolationException("Ese mantenimiento ya está registrado como realizado.");

        var before = new { record.ScheduledAt, record.PerformedAt, record.Description, record.Version };

        _context.Entry(record).Property(r => r.Version).OriginalValue = command.ExpectedVersion;
        record.PerformedAt = command.PerformedAt.ToUniversalTime();
        record.PerformedBy = command.PerformedBy ?? record.PerformedBy ?? userId;
        if (!string.IsNullOrWhiteSpace(command.Description)) record.Description = command.Description.Trim();
        record.PartsUsed = string.IsNullOrWhiteSpace(command.PartsUsed) ? null : command.PartsUsed.Trim();
        record.Cost = command.Cost;
        record.NextMaintenanceAt = command.NextMaintenanceAt?.ToUniversalTime();
        record.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(record).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "maintenance_records",
            recordId: record.MaintenanceRecordId,
            action: "UPDATE",
            oldData: before,
            newData: new { record.AssetId, record.ScheduledAt, record.PerformedAt, record.PerformedBy, record.NextMaintenanceAt, record.Cost, record.Version },
            cancellationToken);

        return new CompleteMaintenanceResponse(record.MaintenanceRecordId, record.PerformedAt!.Value, record.Version);
    }
}
