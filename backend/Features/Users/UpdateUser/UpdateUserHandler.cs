using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Users.Shared;

namespace TecnmInventario.Api.Features.Users.UpdateUser;

public sealed class UpdateUserHandler : ICommandHandler<UpdateUserCommand, UpdateUserResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UpdateUserHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UpdateUserResponse> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var user = await _context.AppUsers.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == command.UserId && !u.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no existe.");

        // Versión vieja -> 409 aunque no cambie nada (regla 15).
        if (user.Version != command.ExpectedVersion)
            throw new DbUpdateConcurrencyException("El usuario fue modificado por otra persona.");

        var roleCode = command.RoleCode.Trim().ToUpperInvariant();
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Code == roleCode && !r.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleViolationException("El rol especificado no existe.");

        var email = command.Email.Trim();
        var firstName = command.FirstName.Trim();
        var lastName = command.LastName.Trim();
        var before = new { user.Email, user.FirstName, user.LastName, Role = user.Role.Code };
        var roleChanges = user.RoleId != role.RoleId;

        // Sin cambios reales: no se escribe ni se audita (regla 15).
        if (before.Email == email && before.FirstName == firstName && before.LastName == lastName && !roleChanges)
            return new UpdateUserResponse(user.UserId, user.Role.Code, user.Version);

        if (email != user.Email && await _context.AppUsers.AnyAsync(u => u.Email == email && u.UserId != user.UserId, cancellationToken))
            throw new BusinessRuleViolationException("Ese correo ya está registrado.");

        if (roleChanges && user.Role.Code == UserAdminGuard.AdminRoleCode)
        {
            if (user.UserId == userId)
                throw new BusinessRuleViolationException("No puedes quitarte a ti mismo el rol de administrador.");
            if (user.Status == UserAdminGuard.ActiveStatus)
                await UserAdminGuard.EnsureNotLastActiveAdminAsync(_context, user, cancellationToken);
        }

        user.Email = email;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.RoleId = role.RoleId;
        user.UpdatedBy = userId;

        // Con rol nuevo, el JWT vigente trae el rol viejo: se cierran sus sesiones
        // para que el siguiente login emita los permisos correctos.
        var revoked = roleChanges ? await UserAdminGuard.RevokeSessionsAsync(_context, user.UserId, cancellationToken) : 0;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(user).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "app_users",
            recordId: user.UserId,
            action: "UPDATE",
            oldData: before,
            newData: new { user.Email, user.FirstName, user.LastName, Role = role.Code, SessionsRevoked = revoked },
            cancellationToken);

        return new UpdateUserResponse(user.UserId, role.Code, user.Version);
    }
}
