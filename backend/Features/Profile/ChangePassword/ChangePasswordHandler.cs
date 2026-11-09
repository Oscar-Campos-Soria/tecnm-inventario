using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Profile.ChangePassword;

/// <summary>
/// El usuario cambia su propia contraseña. Se cierran sus sesiones en otros
/// equipos (otras familias de refresh token); la actual sigue abierta.
/// </summary>
public sealed class ChangePasswordHandler : ICommandHandler<ChangePasswordCommand, ChangePasswordResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthCookieWriter _cookies;
    private readonly IAuditLogService _auditLog;

    public ChangePasswordHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser,
        IAuthCookieWriter cookies, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _cookies = cookies;
        _auditLog = auditLog;
    }

    public async ValueTask<ChangePasswordResponse> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var user = await _context.AppUsers
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no existe.");

        if (!Verify(command.CurrentPassword, user.PasswordHash))
        {
            throw new BusinessRuleViolationException("La contraseña actual no es correcta.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
        user.UpdatedBy = userId;

        var currentFamily = await CurrentFamilyAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var others = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now
                && (currentFamily == null || t.FamilyId != currentFamily))
            .ToListAsync(cancellationToken);
        foreach (var token in others) token.RevokedAt = now;

        // Version y UpdatedAt los pone el trigger touch_version.
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(user).ReloadAsync(cancellationToken);

        // Nunca el hash (regla 16): solo el hecho.
        await _auditLog.LogAsync(
            tableName: "app_users",
            recordId: user.UserId,
            action: "UPDATE",
            oldData: null,
            newData: new { PasswordChanged = true, ChangedBySelf = true, OtherSessionsRevoked = others.Count, user.Version },
            cancellationToken);

        return new ChangePasswordResponse(others.Count);
    }

    // Un hash que no es bcrypt (cuenta técnica) cuenta como contraseña incorrecta, no como error 500.
    private static bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    private async Task<Guid?> CurrentFamilyAsync(CancellationToken ct)
    {
        var raw = _cookies.ReadRefreshToken();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var hash = AuthSessionIssuer.HashToken(raw);
        return await _context.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync(ct);
    }
}
