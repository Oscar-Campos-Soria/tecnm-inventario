using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.GetMaintenance;

namespace TecnmInventario.Api.Features.Maintenance.GetUpcomingMaintenance;

/// <summary>
/// Calendario de preventivos: por cada activo, su preventivo más reciente con
/// próxima fecha dentro de la ventana (o ya vencida). Un preventivo posterior
/// "atiende" al anterior y lo saca de la lista. Activos dados de baja no salen.
/// </summary>
public sealed class GetUpcomingMaintenanceHandler
    : IQueryHandler<GetUpcomingMaintenanceQuery, IReadOnlyList<MaintenanceItem>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetUpcomingMaintenanceHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<MaintenanceItem>> Handle(
        GetUpcomingMaintenanceQuery query, CancellationToken cancellationToken)
    {
        var limit = DateTime.UtcNow.AddDays(query.Days);
        var preventive = _context.MaintenanceRecords.Where(r =>
            !r.IsDeleted && r.MaintenanceType.Code == MaintenanceTypeCodes.Preventivo);

        return await preventive
            .AsNoTracking()
            .Where(r => r.NextMaintenanceAt != null
                && r.NextMaintenanceAt <= limit
                && !r.Asset.IsDeleted
                && r.Asset.AssetStatus.Code != AssetStatusCodes.DadoDeBaja
                && !preventive.Any(later => later.AssetId == r.AssetId
                    && (later.PerformedAt > r.PerformedAt
                        || (later.PerformedAt == r.PerformedAt && later.MaintenanceRecordId > r.MaintenanceRecordId))))
            .OrderBy(r => r.NextMaintenanceAt)
            .Select(MaintenanceProjection.ToItem)
            .ToListAsync(cancellationToken);
    }
}
