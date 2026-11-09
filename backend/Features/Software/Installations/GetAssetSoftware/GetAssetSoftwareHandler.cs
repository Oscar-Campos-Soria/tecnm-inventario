using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Software.Installations.GetAssetSoftware;

public sealed class GetAssetSoftwareHandler
    : IQueryHandler<GetAssetSoftwareQuery, IReadOnlyList<GetAssetSoftwareResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAssetSoftwareHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<GetAssetSoftwareResponse>> Handle(
        GetAssetSoftwareQuery query, CancellationToken cancellationToken)
    {
        if (!await _context.Assets.AnyAsync(a => a.AssetId == query.AssetId && !a.IsDeleted, cancellationToken))
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        var installs = _context.AssetSoftwares.AsNoTracking()
            .Where(i => i.AssetId == query.AssetId && !i.IsDeleted);

        if (!query.IncludeHistory)
        {
            installs = installs.Where(i => i.IsCurrent);
        }

        // Lista por equipo: unas decenas de filas como mucho, sin paginar.
        return await installs
            .OrderByDescending(i => i.IsCurrent).ThenBy(i => i.Software.Name).ThenByDescending(i => i.InstalledAt)
            .Select(i => new GetAssetSoftwareResponse(
                i.AssetSoftwareId,
                i.SoftwareId,
                i.Software.Name,
                i.Software.Vendor,
                i.VersionInstalled,
                i.InstalledAt,
                i.IsCurrent,
                _context.SoftwareLicenseAssignments.Any(a =>
                    a.AssetId == i.AssetId && a.EndedAt == null && a.License.SoftwareId == i.SoftwareId
                    && !a.License.IsDeleted),
                i.Version))
            .ToListAsync(cancellationToken);
    }
}
