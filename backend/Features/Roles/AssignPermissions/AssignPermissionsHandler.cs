using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Roles.AssignPermissions;

public sealed class AssignPermissionsHandler : ICommandHandler<AssignPermissionsCommand, AssignPermissionsResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IRolePermissionService _rolePermissionService;
    private readonly IAuditLogService _auditLog;
    private readonly ITransactionHooks _hooks;

    public AssignPermissionsHandler(
        TecnmInventarioDbContext context,
        IRolePermissionService rolePermissionService,
        IAuditLogService auditLog,
        ITransactionHooks hooks)
    {
        _context = context;
        _rolePermissionService = rolePermissionService;
        _auditLog = auditLog;
        _hooks = hooks;
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

        // Sin altas ni bajas reales no hay escritura: ni SaveChanges, ni
        // audit_log, ni invalidación de caché.
        if (codesToRemove.Count == 0 && codesToAdd.Count == 0)
        {
            var unchangedCodes = role.Permissions.Select(p => p.Code).OrderBy(c => c).ToList();
            return new AssignPermissionsResponse(role.RoleId, role.Code, unchangedCodes);
        }

        // Snapshot "antes", ordenado para que la comparación sea estable.
        var beforeCodes = role.Permissions.Select(p => p.Code).OrderBy(c => c).ToList();

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

        // La caché se invalida SOLO después del commit: si el SaveChanges o
        // audit_log fallan, la transacción se revierte y el hook de commit se
        // descarta, de modo que la caché sigue reflejando el estado real.
        _hooks.OnCommit(() =>
        {
            _rolePermissionService.InvalidateCache(role.Code);
            return Task.CompletedTask;
        });

        await _context.SaveChangesAsync(cancellationToken);

        var finalCodes = role.Permissions.Select(p => p.Code).OrderBy(c => c).ToList();

        await _auditLog.LogAsync(
            tableName: "roles",
            recordId: role.RoleId,
            action: "UPDATE",
            oldData: new { Permissions = beforeCodes },
            newData: new { Permissions = finalCodes },
            cancellationToken);

        return new AssignPermissionsResponse(role.RoleId, role.Code, finalCodes);
    }
}
