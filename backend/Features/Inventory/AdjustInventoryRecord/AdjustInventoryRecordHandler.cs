using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.AdjustInventoryRecord;

/// <summary>
/// Conciliación renglón por renglón (conteo cerrado). El ajuste real se hace
/// con los flujos existentes (movimiento de activo, incidente); aquí se
/// registra y se comprueba que de verdad se aplicó.
/// </summary>
public sealed class AdjustInventoryRecordHandler : ICommandHandler<AdjustInventoryRecordCommand, AdjustInventoryRecordResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public AdjustInventoryRecordHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<AdjustInventoryRecordResponse> Handle(
        AdjustInventoryRecordCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var record = await _context.InventoryRecords
            .Include(r => r.InventorySession)
            .Include(r => r.Asset)
            .FirstOrDefaultAsync(r => r.InventoryRecordId == command.InventoryRecordId
                && r.InventorySessionId == command.InventorySessionId, cancellationToken)
            ?? throw new KeyNotFoundException("El renglón del conteo no existe.");

        if (record.InventorySession.Status != InventorySessionStatusCodes.Cerrada)
            throw new BusinessRuleViolationException("Los ajustes se aplican con el conteo cerrado.");

        if (record.Adjustment is not null)
            throw new BusinessRuleViolationException("Ese renglón ya fue conciliado.");

        var status = InventoryRecordStatusCodes.Of(record.CheckedAt, record.Found, record.ExpectedLocationId, record.LocationFoundId);
        if (!InventoryRecordStatusCodes.NeedsAdjustment(status))
            throw new BusinessRuleViolationException("Ese equipo se encontró en su lugar; no requiere ajuste.");

        switch (command.Adjustment)
        {
            case InventoryAdjustmentCodes.Movimiento:
                if (status == InventoryRecordStatusCodes.NoEncontrado)
                    throw new BusinessRuleViolationException("Un equipo no encontrado no se puede mover.");
                if (record.Asset.CurrentLocationId != record.LocationFoundId)
                    throw new BusinessRuleViolationException(
                        "Primero registra el movimiento del equipo a la ubicación donde se encontró.");
                break;

            case InventoryAdjustmentCodes.Incidente:
                if (status != InventoryRecordStatusCodes.NoEncontrado)
                    throw new BusinessRuleViolationException("El incidente aplica solo a equipos no encontrados.");
                var incidentAssetId = await _context.Incidents
                    .Where(i => i.IncidentId == command.IncidentId)
                    .Select(i => (long?)i.AssetId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (incidentAssetId != record.AssetId)
                    throw new BusinessRuleViolationException("El incidente no existe o es de otro equipo.");
                break;
        }

        _context.Entry(record).Property(r => r.Version).OriginalValue = command.ExpectedVersion;
        record.Adjustment = command.Adjustment;
        record.AdjustedAt = DateTime.UtcNow;
        record.AdjustedBy = userId;
        record.IncidentId = command.Adjustment == InventoryAdjustmentCodes.Incidente ? command.IncidentId : null;
        record.UpdatedBy = userId;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(record).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "inventory_records",
            recordId: record.InventoryRecordId,
            action: "UPDATE",
            oldData: new { Status = status, Adjustment = (string?)null },
            newData: new { record.Adjustment, record.IncidentId, record.AssetId },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new AdjustInventoryRecordResponse(record.InventoryRecordId, record.Adjustment!, record.Version);
    }
}
