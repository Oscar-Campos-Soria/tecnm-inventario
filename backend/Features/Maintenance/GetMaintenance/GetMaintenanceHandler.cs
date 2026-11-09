using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenance;

public sealed class GetMaintenanceHandler : IQueryHandler<GetMaintenanceQuery, PagedResult<MaintenanceItem>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetMaintenanceHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<MaintenanceItem>> Handle(
        GetMaintenanceQuery query, CancellationToken cancellationToken)
    {
        // maintenance_records no tiene filtro global de borrado: se excluye aquí.
        var records = _context.MaintenanceRecords.AsNoTracking().Where(r => !r.IsDeleted && r.PerformedAt != null);

        if (query.AssetId.HasValue)
        {
            var assetId = query.AssetId.Value;
            records = records.Where(r => r.AssetId == assetId);
        }

        if (!string.IsNullOrWhiteSpace(query.TypeCode))
        {
            var code = query.TypeCode.Trim().ToUpperInvariant();
            records = records.Where(r => r.MaintenanceType.Code == code);
        }

        var totalCount = await records.CountAsync(cancellationToken);

        var items = await records
            .OrderByDescending(r => r.PerformedAt)
            .ThenByDescending(r => r.MaintenanceRecordId)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MaintenanceProjection.ToItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<MaintenanceItem>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
