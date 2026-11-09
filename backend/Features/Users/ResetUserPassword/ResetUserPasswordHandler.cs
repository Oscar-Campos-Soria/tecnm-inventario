using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Users.Shared;

namespace TecnmInventario.Api.Features.Users.ResetUserPassword;

/// <summary>El admin fija una contraseña nueva; las sesiones abiertas se cierran.</summary>
public sealed class ResetUserPasswordHandler : ICommandHandler<ResetUserPasswordCommand, ResetUserPasswordResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public ResetUserPasswordHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<ResetUserPasswordResponse> Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var user = await _context.AppUsers
            .FirstOrDefaultAsync(u => u.UserId == command.UserId && !u.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no existe.");

        if (user.Version != command.ExpectedVersion)
            throw new DbUpdateConcurrencyException("El usuario fue modificado por otra persona.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
        user.UpdatedBy = userId;

        var revoked = await UserAdminGuard.RevokeSessionsAsync(_context, user.UserId, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(user).ReloadAsync(cancellationToken);

        // Nunca el hash (regla 16): solo el hecho.
        await _auditLog.LogAsync(
            tableName: "app_users",
            recordId: user.UserId,
            action: "UPDATE",
            oldData: null,
            newData: new { PasswordChanged = true, SessionsRevoked = revoked },
            cancellationToken);

        return new ResetUserPasswordResponse(user.UserId, revoked, user.Version);
    }
}
