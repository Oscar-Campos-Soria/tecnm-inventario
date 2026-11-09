using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Users.Shared;

namespace TecnmInventario.Api.Features.Users.ChangeUserStatus;

/// <summary>Activa, desactiva o suspende. Quien deja de estar activo pierde sus sesiones.</summary>
public sealed class ChangeUserStatusHandler : ICommandHandler<ChangeUserStatusCommand, ChangeUserStatusResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public ChangeUserStatusHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<ChangeUserStatusResponse> Handle(ChangeUserStatusCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var user = await _context.AppUsers.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == command.UserId && !u.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no existe.");

        if (user.Version != command.ExpectedVersion)
            throw new DbUpdateConcurrencyException("El usuario fue modificado por otra persona.");

        if (user.Status == command.Status)
            return new ChangeUserStatusResponse(user.UserId, user.Status, 0, user.Version);

        if (user.Status == UserAdminGuard.ActiveStatus)
        {
            if (user.UserId == userId)
                throw new BusinessRuleViolationException("No puedes desactivar tu propia cuenta.");
            if (user.Role.Code == UserAdminGuard.AdminRoleCode)
                await UserAdminGuard.EnsureNotLastActiveAdminAsync(_context, user, cancellationToken);
        }

        var previous = user.Status;
        user.Status = command.Status;
        user.UpdatedBy = userId;

        var revoked = command.Status == UserAdminGuard.ActiveStatus
            ? 0
            : await UserAdminGuard.RevokeSessionsAsync(_context, user.UserId, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(user).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "app_users",
            recordId: user.UserId,
            action: "UPDATE",
            oldData: new { Status = previous },
            newData: new { user.Status, SessionsRevoked = revoked },
            cancellationToken);

        return new ChangeUserStatusResponse(user.UserId, user.Status, revoked, user.Version);
    }
}
