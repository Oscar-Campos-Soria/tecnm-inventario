using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.GetInventorySessionById;

public sealed class GetInventorySessionByIdHandler : IQueryHandler<GetInventorySessionByIdQuery, InventorySessionDetail>
{
    private readonly TecnmInventarioDbContext _context;

    public GetInventorySessionByIdHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<InventorySessionDetail> Handle(
        GetInventorySessionByIdQuery query, CancellationToken cancellationToken)
    {
        var session = await _context.InventorySessions.AsNoTracking()
            .Where(s => s.InventorySessionId == query.InventorySessionId)
            .Select(s => new
            {
                s.InventorySessionId, s.Name, s.LocationId,
                LocationName = s.Location != null ? s.Location.Name : null,
                s.Status, s.StartedAt, s.EndedAt, s.Version
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("El conteo solicitado no existe.");

        var rows = await _context.InventoryRecords.AsNoTracking()
            .Where(r => r.InventorySessionId == session.InventorySessionId)
            .OrderBy(r => r.Asset.InventoryCode)
            .Select(r => new
            {
                r.InventoryRecordId, r.AssetId, r.Asset.InventoryCode,
                AssetTypeName = r.Asset.AssetType.Name,
                AssetStatusCode = r.Asset.AssetStatus.Code,
                AssetVersion = r.Asset.Version,
                r.Asset.CurrentLocationId,
                r.ExpectedLocationId,
                ExpectedLocationName = r.ExpectedLocation != null ? r.ExpectedLocation.Name : null,
                r.LocationFoundId,
                LocationFoundName = r.LocationFound != null ? r.LocationFound.Name : null,
                r.Found, r.CheckedAt,
                CheckedByUsername = _context.AppUsers.Where(u => u.UserId == r.CheckedBy).Select(u => u.Username).FirstOrDefault(),
                r.Notes, r.Adjustment, r.IncidentId, r.Version
            })
            .ToListAsync(cancellationToken);

        var records = rows.Select(r => new InventoryRecordItem(
                r.InventoryRecordId, r.AssetId, r.InventoryCode, r.AssetTypeName,
                r.AssetStatusCode, r.AssetVersion, r.CurrentLocationId,
                r.ExpectedLocationId, r.ExpectedLocationName, r.LocationFoundId, r.LocationFoundName,
                InventoryRecordStatusCodes.Of(r.CheckedAt, r.Found, r.ExpectedLocationId, r.LocationFoundId),
                r.CheckedAt, r.CheckedByUsername, r.Notes, r.Adjustment, r.IncidentId, r.Version))
            .ToList();

        var summary = records.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());

        return new InventorySessionDetail(
            session.InventorySessionId, session.Name, session.LocationId, session.LocationName,
            session.Status, session.StartedAt, session.EndedAt, summary, records, session.Version);
    }
}
