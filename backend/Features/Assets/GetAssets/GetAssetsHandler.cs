using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Locations.Shared;

namespace TecnmInventario.Api.Features.Assets.GetAssets;

public sealed class GetAssetsHandler : IQueryHandler<GetAssetsQuery, PagedResult<GetAssetsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAssetsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetAssetsResponse>> Handle(
        GetAssetsQuery query, CancellationToken cancellationToken)
    {
        var assets = _context.Assets.AsNoTracking().Where(a => !a.IsDeleted);

        var statusCode = string.IsNullOrWhiteSpace(query.StatusCode) ? null : query.StatusCode.Trim().ToUpperInvariant();
        if (statusCode is not null)
            assets = assets.Where(a => a.AssetStatus.Code == statusCode);
        else if (!query.IncludeDisposed)
            assets = assets.Where(a => a.AssetStatus.Code != AssetStatusCodes.DadoDeBaja);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            assets = assets.Where(a =>
                a.InventoryCode.ToLower().Contains(term)
                || (a.SerialNumber != null && a.SerialNumber.ToLower().Contains(term))
                || (a.Hostname != null && a.Hostname.ToLower().Contains(term)));
        }

        if (query.AssetTypeId.HasValue)
        {
            var typeId = query.AssetTypeId.Value;
            assets = assets.Where(a => a.AssetTypeId == typeId);
        }

        if (query.LocationId.HasValue)
        {
            var locationIds = await LocationTree.DescendantsAndSelfAsync(_context, query.LocationId.Value, cancellationToken);
            assets = assets.Where(a => a.CurrentLocationId != null && locationIds.Contains(a.CurrentLocationId.Value));
        }

        var totalCount = await assets.CountAsync(cancellationToken);

        var items = await assets
            .OrderBy(a => a.InventoryCode)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new GetAssetsResponse(
                a.AssetId,
                a.InventoryCode,
                a.SerialNumber,
                a.Hostname,
                a.Version,
                a.AssetType.Name,
                a.AssetStatus.Code,
                a.AssetStatus.Name,
                a.CurrentLocationId,
                a.CurrentLocation != null ? a.CurrentLocation.Name : null))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetAssetsResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
