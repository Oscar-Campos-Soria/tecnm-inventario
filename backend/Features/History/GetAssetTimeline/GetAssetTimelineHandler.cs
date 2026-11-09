using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.History.GetAuditLog;

namespace TecnmInventario.Api.Features.History.GetAssetTimeline;

/// <summary>
/// Línea de tiempo de un activo: sus propias filas de auditoría más las de
/// otras tablas cuyo snapshot menciona su AssetId (movimientos, asignaciones,
/// incidentes, mantenimientos, baja, conteos...). Últimos 200 eventos.
/// </summary>
public sealed class GetAssetTimelineHandler : IQueryHandler<GetAssetTimelineQuery, IReadOnlyList<AuditLogItem>>
{
    private const int MaxEvents = 200;
    private readonly TecnmInventarioDbContext _context;

    public GetAssetTimelineHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<AuditLogItem>> Handle(GetAssetTimelineQuery query, CancellationToken cancellationToken)
    {
        if (!await _context.Assets.AnyAsync(a => a.AssetId == query.AssetId, cancellationToken))
            throw new KeyNotFoundException("El activo solicitado no existe.");

        var assetId = query.AssetId;
        var idText = assetId.ToString();

        // Los snapshots se serializan con System.Text.Json (PascalCase): la
        // clave es "AssetId". Parámetros interpolados: sin inyección.
        return await _context.AuditLogs
            .FromSqlInterpolated($@"SELECT * FROM inventario.audit_log
                WHERE (table_name = 'assets' AND record_id = {assetId})
                   OR new_data ->> 'AssetId' = {idText}
                   OR old_data ->> 'AssetId' = {idText}")
            .AsNoTracking()
            .OrderByDescending(l => l.AuditLogId)
            .Take(MaxEvents)
            .Select(l => new AuditLogItem(
                l.AuditLogId, l.TableName, l.RecordId, l.Action, l.PerformedBy,
                l.PerformedByNavigation != null ? l.PerformedByNavigation.Username : null,
                l.PerformedAt, l.OldData, l.NewData))
            .ToListAsync(cancellationToken);
    }
}
