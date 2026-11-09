using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.CancelInventorySession;

/// <summary>
/// Descarta un conteo abierto (se inició por error o en la ubicación equivocada).
/// No toca activos ni registros: quedan como historial, pero un conteo cancelado
/// no se concilia y libera la ubicación para abrir uno nuevo. El motivo va a audit_log
/// (inventory_sessions no tiene columna para guardarlo).
/// </summary>
public sealed class CancelInventorySessionHandler
    : ICommandHandler<CancelInventorySessionCommand, CancelInventorySessionResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CancelInventorySessionHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CancelInventorySessionResponse> Handle(
        CancelInventorySessionCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var session = await _context.InventorySessions
            .FirstOrDefaultAsync(s => s.InventorySessionId == command.InventorySessionId, cancellationToken)
            ?? throw new KeyNotFoundException("El conteo solicitado no existe.");

        if (session.Version != command.ExpectedVersion)
            throw new DbUpdateConcurrencyException("El conteo fue modificado por otra persona.");

        if (session.Status != InventorySessionStatusCodes.Abierta)
            throw new BusinessRuleViolationException("Solo se puede cancelar un conteo abierto.");

        _context.Entry(session).Property(s => s.Version).OriginalValue = command.ExpectedVersion;
        session.Status = InventorySessionStatusCodes.Cancelada;
        session.EndedAt = DateTime.UtcNow;
        session.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(session).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "inventory_sessions",
            recordId: session.InventorySessionId,
            action: "UPDATE",
            oldData: new { Status = InventorySessionStatusCodes.Abierta },
            newData: new { session.Status, session.EndedAt, Reason = command.Reason.Trim(), session.Version },
            cancellationToken);

        return new CancelInventorySessionResponse(session.InventorySessionId, session.Status, session.Version);
    }
}
