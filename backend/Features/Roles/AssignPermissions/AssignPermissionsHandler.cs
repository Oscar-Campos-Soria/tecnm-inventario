using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Roles.AssignPermissions;

public sealed class AssignPermissionsHandler : ICommandHandler<AssignPermissionsCommand, AssignPermissionsResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IRolePermissionService _rolePermissionService;

    public AssignPermissionsHandler(TecnmInventarioDbContext context, IRolePermissionService rolePermissionService)
    {
        _context = context;
        _rolePermissionService = rolePermissionService;
    }

    public async ValueTask<AssignPermissionsResponse> Handle(
        AssignPermissionsCommand command, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .Include(r => r.Permissions)
            .FirstAsync(r => r.RoleId == command.RoleId && !r.IsDeleted, cancellationToken);

        var desiredCodes = command.PermissionCodes.ToHashSet();
        var currentCodes = role.Permissions.Select(p => p.Code).ToHashSet();

        var codesToRemove = currentCodes.Except(desiredCodes).ToList();
        var codesToAdd = desiredCodes.Except(currentCodes).ToList();

        if (codesToRemove.Count > 0)
        {
            var permissionsToRemove = role.Permissions
                .Where(p => codesToRemove.Contains(p.Code))
                .ToList();

            foreach (var permission in permissionsToRemove)
            {
                role.Permissions.Remove(permission);
            }
        }

        if (codesToAdd.Count > 0)
        {
            var permissionsToAdd = await _context.Permissions
                .Where(p => codesToAdd.Contains(p.Code) && !p.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var permission in permissionsToAdd)
            {
                role.Permissions.Add(permission);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidar la caché de permisos de este rol para que el cambio
        // surta efecto de inmediato, sin esperar el TTL de 5 minutos.
        _rolePermissionService.InvalidateCache(role.Code);

        var finalCodes = role.Permissions.Select(p => p.Code).OrderBy(c => c).ToList();
        return new AssignPermissionsResponse(role.RoleId, role.Code, finalCodes);
    }
}
