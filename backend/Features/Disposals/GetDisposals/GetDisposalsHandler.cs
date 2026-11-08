using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Disposals.GetDisposals;

public sealed class GetDisposalsHandler : IQueryHandler<GetDisposalsQuery, PagedResult<GetDisposalsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetDisposalsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetDisposalsResponse>> Handle(
        GetDisposalsQuery query, CancellationToken cancellationToken)
    {
        var disposals = _context.AssetDisposals.AsNoTracking();

        if (query.AssetId.HasValue)
        {
            var assetId = query.AssetId.Value;
            disposals = disposals.Where(d => d.AssetId == assetId);
        }

        if (query.DisposalReasonId.HasValue)
        {
            var reasonId = query.DisposalReasonId.Value;
            disposals = disposals.Where(d => d.DisposalReasonId == reasonId);
        }

        // Busca por folio o código de inventario.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            disposals = disposals.Where(d =>
                d.Folio.ToLower().Contains(term) || d.Asset.InventoryCode.ToLower().Contains(term));
        }

        var totalCount = await disposals.CountAsync(cancellationToken);

        var items = await disposals
            .OrderByDescending(d => d.DisposedAt)
            .ThenByDescending(d => d.AssetDisposalId)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(d => new GetDisposalsResponse(
                d.AssetDisposalId,
                d.AssetId,
                d.Asset.InventoryCode,
                d.Asset.AssetType.Name,
                d.DisposalReason.Code,
                d.DisposalReason.Name,
                d.Folio,
                d.Dictamen,
                d.DisposedAt,
                d.CreatedByNavigation != null ? d.CreatedByNavigation.Username : null,
                d.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetDisposalsResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
