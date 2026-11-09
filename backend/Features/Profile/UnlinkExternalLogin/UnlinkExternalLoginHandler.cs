using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Profile.UnlinkExternalLogin;

/// <summary>
/// Quita el vínculo con Microsoft o Google de la cuenta propia. La cuenta
/// conserva su contraseña, así que sigue pudiendo entrar. user_external_logins
/// no tiene baja lógica: se borra la fila y queda el DELETE en audit_log.
/// </summary>
public sealed class UnlinkExternalLoginHandler : ICommandHandler<UnlinkExternalLoginCommand>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UnlinkExternalLoginHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<Unit> Handle(UnlinkExternalLoginCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var provider = command.Provider.Trim().ToLowerInvariant();

        var link = await _context.UserExternalLogins
            .FirstOrDefaultAsync(l => l.UserId == userId && l.Provider == provider, cancellationToken)
            ?? throw new KeyNotFoundException("Tu cuenta no está vinculada con ese proveedor.");

        var snapshot = new { link.UserId, link.Provider, link.Email, link.LinkedAt };
        _context.UserExternalLogins.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "user_external_logins",
            recordId: link.UserExternalLoginId,
            action: "DELETE",
            oldData: snapshot,
            newData: null,
            cancellationToken);

        return Unit.Value;
    }
}
