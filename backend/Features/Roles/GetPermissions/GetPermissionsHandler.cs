using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Roles.GetPermissions;

/// <summary>Catálogo completo de permisos para marcar los de cada rol.</summary>
public sealed class GetPermissionsHandler : IQueryHandler<GetPermissionsQuery, IReadOnlyList<PermissionItem>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetPermissionsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<PermissionItem>> Handle(GetPermissionsQuery query, CancellationToken cancellationToken)
    {
        var rows = await _context.Permissions.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Code)
            .Select(p => new { p.Code, p.Name, p.Description })
            .ToListAsync(cancellationToken);

        return rows
            .Select(p => new PermissionItem(p.Code, p.Name, p.Description, p.Code.Split('.')[0]))
            .ToList();
    }
}
