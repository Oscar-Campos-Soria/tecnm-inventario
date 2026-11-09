using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Components.GetAssetComponents;

public sealed class GetAssetComponentsHandler
    : IQueryHandler<GetAssetComponentsQuery, IReadOnlyList<GetAssetComponentsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAssetComponentsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<GetAssetComponentsResponse>> Handle(
        GetAssetComponentsQuery query, CancellationToken cancellationToken)
    {
        if (!await _context.Assets.AnyAsync(a => a.AssetId == query.AssetId && !a.IsDeleted, cancellationToken))
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        var components = _context.AssetComponents.AsNoTracking().Where(c => c.AssetId == query.AssetId);
        if (!query.IncludeRemoved)
        {
            components = components.Where(c => c.RemovedAt == null);
        }

        // Por equipo son pocas filas: sin paginar.
        return await components
            .OrderBy(c => c.RemovedAt != null)
            .ThenBy(c => c.ComponentType.Name)
            .ThenByDescending(c => c.InstalledAt)
            .Select(c => new GetAssetComponentsResponse(
                c.AssetComponentId,
                c.ComponentTypeId,
                c.ComponentType.Code,
                c.ComponentType.Name,
                c.Description,
                c.SerialNumber,
                c.InstalledAt,
                c.RemovedAt,
                c.Version))
            .ToListAsync(cancellationToken);
    }
}
