using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Users.Shared;

/// <summary>
/// Protecciones anti-bloqueo de la administración de usuarios: el sistema
/// nunca se queda sin un ADMIN activo y un admin no se quita a sí mismo
/// el acceso. Además cierra las sesiones de quien deja de poder entrar.
/// </summary>
public static class UserAdminGuard
{
    public const string AdminRoleCode = "ADMIN";
    public const string ActiveStatus = "activo";

    /// <summary>Falla si sacar a <paramref name="user"/> de admin activo deja al sistema sin admins.</summary>
    public static async Task EnsureNotLastActiveAdminAsync(
        TecnmInventarioDbContext context, AppUser user, CancellationToken cancellationToken)
    {
        var others = await context.AppUsers.CountAsync(
            u => u.UserId != user.UserId && !u.IsDeleted && u.Status == ActiveStatus && u.Role.Code == AdminRoleCode,
            cancellationToken);
        if (others == 0)
            throw new BusinessRuleViolationException("Debe quedar al menos un administrador activo.");
    }

    /// <summary>Revoca los refresh tokens vigentes: la sesión ya no se puede renovar.</summary>
    public static async Task<int> RevokeSessionsAsync(
        TecnmInventarioDbContext context, long userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tokens = await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens) token.RevokedAt = now;
        return tokens.Count;
    }
}
