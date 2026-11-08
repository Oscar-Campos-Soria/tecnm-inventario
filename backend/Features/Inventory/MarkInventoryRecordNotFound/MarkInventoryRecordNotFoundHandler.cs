using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.MarkInventoryRecordNotFound;

/// <summary>Marca un equipo como no encontrado (también corrige un escaneo equivocado).</summary>
public sealed class MarkInventoryRecordNotFoundHandler
    : ICommandHandler<MarkInventoryRecordNotFoundCommand, MarkInventoryRecordNotFoundResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public MarkInventoryRecordNotFoundHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<MarkInventoryRecordNotFoundResponse> Handle(
        MarkInventoryRecordNotFoundCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var record = await _context.InventoryRecords
            .Include(r => r.InventorySession)
            .FirstOrDefaultAsync(r => r.InventoryRecordId == command.InventoryRecordId
                && r.InventorySessionId == command.InventorySessionId, cancellationToken)
            ?? throw new KeyNotFoundException("El renglón del conteo no existe.");

        if (record.InventorySession.Status != InventorySessionStatusCodes.Abierta)
            throw new BusinessRuleViolationException("El conteo ya está cerrado.");

        // Un sobrante (no estaba en la lista) se corrige quitándolo, no marcándolo como faltante.
        if (record.ExpectedLocationId is null)
            throw new BusinessRuleViolationException("Ese equipo no estaba en la lista esperada.");

        var previous = new { record.Found, record.LocationFoundId, record.CheckedAt };

        _context.Entry(record).Property(r => r.Version).OriginalValue = command.ExpectedVersion;
        record.Found = false;
        record.LocationFoundId = null;
        record.CheckedAt = DateTime.UtcNow;
        record.CheckedBy = userId;
        record.UpdatedBy = userId;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(record).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "inventory_records",
            recordId: record.InventoryRecordId,
            action: "UPDATE",
            oldData: previous,
            newData: new { record.Found, Status = InventoryRecordStatusCodes.NoEncontrado },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new MarkInventoryRecordNotFoundResponse(record.InventoryRecordId, InventoryRecordStatusCodes.NoEncontrado, record.Version);
    }
}
