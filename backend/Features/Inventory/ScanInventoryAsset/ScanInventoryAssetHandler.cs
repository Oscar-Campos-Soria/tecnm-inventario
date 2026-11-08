using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.ScanInventoryAsset;

public sealed class ScanInventoryAssetHandler : ICommandHandler<ScanInventoryAssetCommand, ScanInventoryAssetResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public ScanInventoryAssetHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<ScanInventoryAssetResponse> Handle(
        ScanInventoryAssetCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var session = await _context.InventorySessions
            .FirstOrDefaultAsync(s => s.InventorySessionId == command.InventorySessionId, cancellationToken)
            ?? throw new KeyNotFoundException("El conteo solicitado no existe.");
        if (session.Status != InventorySessionStatusCodes.Abierta)
            throw new BusinessRuleViolationException("El conteo ya está cerrado.");

        var locationFoundId = command.LocationFoundId ?? session.LocationId;
        if (locationFoundId.HasValue
            && !await _context.Locations.AnyAsync(l => l.LocationId == locationFoundId.Value, cancellationToken))
            throw new BusinessRuleViolationException("La ubicación indicada no existe.");

        var code = command.InventoryCode.Trim().ToUpper();
        var asset = await _context.Assets
            .Where(a => !a.IsDeleted && a.InventoryCode.ToUpper() == code)
            .Select(a => new { a.AssetId, a.InventoryCode, StatusCode = a.AssetStatus.Code })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException(
                $"No hay ningún activo con el código {command.InventoryCode.Trim()}. Si es un equipo nuevo, regístralo primero en Activos.");

        var record = await _context.InventoryRecords.FirstOrDefaultAsync(
            r => r.InventorySessionId == session.InventorySessionId && r.AssetId == asset.AssetId, cancellationToken);

        if (record is { CheckedAt: not null, Found: true })
        {
            return new ScanInventoryAssetResponse(
                record.InventoryRecordId, asset.AssetId, asset.InventoryCode,
                InventoryRecordStatusCodes.Of(record.CheckedAt, record.Found, record.ExpectedLocationId, record.LocationFoundId),
                AlreadyChecked: true, asset.StatusCode);
        }

        var notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();
        var isNew = record is null;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (record is null)
        {
            // Sobrante: apareció en el área pero no estaba en la lista esperada.
            record = new InventoryRecord
            {
                InventorySessionId = session.InventorySessionId,
                AssetId = asset.AssetId,
                ExpectedLocationId = null,
                CreatedBy = userId,
                Version = 1
            };
            _context.InventoryRecords.Add(record);
        }

        record.Found = true;
        record.LocationFoundId = locationFoundId;
        record.CheckedAt = DateTime.UtcNow;
        record.CheckedBy = userId;
        record.Notes = notes ?? record.Notes;
        record.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);
        if (!isNew) await _context.Entry(record).ReloadAsync(cancellationToken);

        var status = InventoryRecordStatusCodes.Of(record.CheckedAt, record.Found, record.ExpectedLocationId, record.LocationFoundId);

        await _auditLog.LogAsync(
            tableName: "inventory_records",
            recordId: record.InventoryRecordId,
            action: isNew ? "INSERT" : "UPDATE",
            oldData: null,
            newData: new { record.InventorySessionId, record.AssetId, record.Found, record.LocationFoundId, Status = status },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ScanInventoryAssetResponse(
            record.InventoryRecordId, asset.AssetId, asset.InventoryCode, status, AlreadyChecked: false, asset.StatusCode);
    }
}
