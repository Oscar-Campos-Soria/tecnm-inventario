using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.GetMyAssets;

public sealed class GetMyAssetsHandler : IQueryHandler<GetMyAssetsQuery, IReadOnlyList<MyAssetResponse>>
{
    // Tickets recientes por equipo en la tarjeta; el resto está en "Tickets".
    private const int TicketsPerAsset = 5;

    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyAssetsHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<IReadOnlyList<MyAssetResponse>> Handle(GetMyAssetsQuery query, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Sesión no válida.");

        var assets = await _context.AssetAssignments.AsNoTracking()
            .Where(a => a.AssignedTo == userId && a.ReturnedAt == null && !a.Asset.IsDeleted)
            .OrderBy(a => a.Asset.InventoryCode)
            .Select(a => new
            {
                a.AssetId,
                a.Asset.InventoryCode,
                TypeName = a.Asset.AssetType.Name,
                ManufacturerName = a.Asset.Manufacturer != null ? a.Asset.Manufacturer.Name : null,
                ModelName = a.Asset.Model != null ? a.Asset.Model.ModelName : null,
                a.Asset.SerialNumber,
                StatusCode = a.Asset.AssetStatus.Code,
                StatusName = a.Asset.AssetStatus.Name,
                LocationName = a.Asset.CurrentLocation != null ? a.Asset.CurrentLocation.Name : null,
                AssignmentTypeName = a.AssignmentType.Name,
                a.AssignedAt,
            })
            .ToListAsync(cancellationToken);

        var ids = assets.Select(a => a.AssetId).ToList();
        var tickets = await _context.Tickets.AsNoTracking()
            .Where(t => t.RequesterId == userId && t.AssetId != null && ids.Contains(t.AssetId.Value))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { AssetId = t.AssetId!.Value, t.TicketId, t.Title, t.Status, t.CreatedAt })
            .ToListAsync(cancellationToken);
        var byAsset = tickets.GroupBy(t => t.AssetId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<MyAssetTicket>)g.Take(TicketsPerAsset).Select(t => new MyAssetTicket(t.TicketId, t.Title, t.Status, t.CreatedAt)).ToList());

        return assets.Select(a => new MyAssetResponse(a.AssetId, a.InventoryCode, a.TypeName, a.ManufacturerName, a.ModelName,
                a.SerialNumber, a.StatusCode, a.StatusName, a.LocationName, a.AssignmentTypeName, a.AssignedAt,
                byAsset.GetValueOrDefault(a.AssetId, [])))
            .ToList();
    }
}
