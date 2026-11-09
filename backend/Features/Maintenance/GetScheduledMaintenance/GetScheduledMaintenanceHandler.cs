using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.GetMaintenance;

namespace TecnmInventario.Api.Features.Maintenance.GetScheduledMaintenance;

public sealed class GetScheduledMaintenanceHandler
    : IQueryHandler<GetScheduledMaintenanceQuery, IReadOnlyList<MaintenanceItem>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetScheduledMaintenanceHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<MaintenanceItem>> Handle(
        GetScheduledMaintenanceQuery query, CancellationToken cancellationToken)
    {
        var limit = DateTime.UtcNow.AddDays(query.Days);
        return await _context.MaintenanceRecords.AsNoTracking()
            .Where(r => !r.IsDeleted && r.PerformedAt == null && r.ScheduledAt != null && r.ScheduledAt <= limit
                && !r.Asset.IsDeleted && r.Asset.AssetStatus.Code != AssetStatusCodes.DadoDeBaja)
            .OrderBy(r => r.ScheduledAt)
            .Select(MaintenanceProjection.ToItem)
            .ToListAsync(cancellationToken);
    }
}
