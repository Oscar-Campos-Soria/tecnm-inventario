using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.CloseInventorySession;

/// <summary>
/// Cierra el conteo: lo que quedó sin revisar cuenta como no encontrado.
/// No cambia activos: los ajustes se confirman uno por uno al conciliar.
/// </summary>
public sealed class CloseInventorySessionHandler : ICommandHandler<CloseInventorySessionCommand, CloseInventorySessionResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CloseInventorySessionHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CloseInventorySessionResponse> Handle(
        CloseInventorySessionCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var session = await _context.InventorySessions
            .FirstOrDefaultAsync(s => s.InventorySessionId == command.InventorySessionId, cancellationToken)
            ?? throw new KeyNotFoundException("El conteo solicitado no existe.");

        if (session.Status != InventorySessionStatusCodes.Abierta)
            throw new BusinessRuleViolationException("El conteo ya está cerrado.");

        var pending = await _context.InventoryRecords
            .Where(r => r.InventorySessionId == session.InventorySessionId && r.CheckedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var record in pending)
        {
            record.Found = false;
            record.CheckedAt = now;
            record.CheckedBy = userId;
            record.Notes ??= "Sin revisar al cerrar el conteo";
            record.UpdatedBy = userId;
        }

        _context.Entry(session).Property(s => s.Version).OriginalValue = command.ExpectedVersion;
        session.Status = InventorySessionStatusCodes.Cerrada;
        session.EndedAt = now;
        session.UpdatedBy = userId;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(session).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "inventory_sessions",
            recordId: session.InventorySessionId,
            action: "UPDATE",
            oldData: new { Status = InventorySessionStatusCodes.Abierta },
            newData: new { session.Status, session.EndedAt, PendingMarkedNotFound = pending.Count },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CloseInventorySessionResponse(session.InventorySessionId, session.Status, pending.Count, session.Version);
    }
}
