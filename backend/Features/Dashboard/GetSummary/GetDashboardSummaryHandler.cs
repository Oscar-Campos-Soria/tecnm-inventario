using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.GetUpcomingMaintenance;

namespace TecnmInventario.Api.Features.Dashboard.GetSummary;

/// <summary>
/// Métricas del panel. Se cachean 60 s en memoria: son agregados de varias
/// tablas y el panel se abre en cada inicio de sesión. Un cambio puede tardar
/// hasta un minuto en reflejarse.
/// </summary>
public sealed class GetDashboardSummaryHandler
    : IQueryHandler<GetDashboardSummaryQuery, GetDashboardSummaryResponse>
{
    public const int WindowDays = 30;
    private const string CacheKey = "dashboard:summary";
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    private readonly TecnmInventarioDbContext _context;
    private readonly IMemoryCache _cache;

    public GetDashboardSummaryHandler(TecnmInventarioDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async ValueTask<GetDashboardSummaryResponse> Handle(
        GetDashboardSummaryQuery query, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out GetDashboardSummaryResponse? cached) && cached is not null)
        {
            return cached;
        }

        var summary = await BuildAsync(cancellationToken);
        _cache.Set(CacheKey, summary, CacheFor);
        return summary;
    }

    private async Task<GetDashboardSummaryResponse> BuildAsync(CancellationToken ct)
    {
        // v_asset_summary ya excluye activos borrados lógicamente.
        var assets = _context.VAssetSummaries.AsNoTracking();
        var inService = assets.Where(a => a.Status != AssetStatusCodes.DadoDeBaja);

        var byStatus = await assets
            .GroupBy(a => new { a.Status, a.StatusName })
            .Select(g => new CountItem(g.Key.Status!, g.Key.StatusName!, g.Count()))
            .ToListAsync(ct);

        var byType = await inService
            .GroupBy(a => new { a.AssetType, a.AssetTypeName })
            .Select(g => new CountItem(g.Key.AssetType!, g.Key.AssetTypeName!, g.Count()))
            .ToListAsync(ct);

        // Por ubicación desde v_asset_summary y no desde v_location_equipment_summary:
        // esa vista cuenta también los equipos dados de baja.
        var byLocation = await inService
            .Where(a => a.LocationCode != null)
            .GroupBy(a => new { a.LocationCode, a.LocationName })
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.LocationName)
            .Take(10)
            .Select(g => new CountItem(g.Key.LocationCode!, g.Key.LocationName!, g.Count()))
            .ToListAsync(ct);

        var closed = new[] { IncidentStatusCodes.Resuelto, IncidentStatusCodes.Cerrado };
        var incidents = await _context.IncidentPriorities.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.IncidentPriorityId)
            .Select(p => new CountItem(
                p.Code,
                p.Name,
                p.Incidents.Count(i => !closed.Contains(i.IncidentStatus.Code))))
            .ToListAsync(ct);

        // Misma regla que el calendario de /maintenance (preventivo más reciente por activo).
        var upcoming = await new GetUpcomingMaintenanceHandler(_context)
            .Handle(new GetUpcomingMaintenanceQuery(WindowDays), ct);
        var now = DateTime.UtcNow;
        var overdue = upcoming.Count(m => m.NextMaintenanceAt < now);

        var today = DateOnly.FromDateTime(now);
        var limit = today.AddDays(WindowDays);
        var licensesExpired = await _context.SoftwareLicenses.CountAsync(l => l.ValidUntil < today, ct);
        var licensesSoon = await _context.SoftwareLicenses
            .CountAsync(l => l.ValidUntil >= today && l.ValidUntil <= limit, ct);

        return new GetDashboardSummaryResponse(
            TotalAssets: byStatus.Where(s => s.Code != AssetStatusCodes.DadoDeBaja).Sum(s => s.Count),
            AssetsByStatus: byStatus.OrderByDescending(s => s.Count).ToList(),
            AssetsByType: byType.OrderByDescending(t => t.Count).ThenBy(t => t.Name).ToList(),
            OpenIncidentsByPriority: incidents,
            PreventiveOverdue: overdue,
            PreventiveDueSoon: upcoming.Count - overdue,
            AssetsByLocation: byLocation,
            LicensesExpired: licensesExpired,
            LicensesExpiringSoon: licensesSoon,
            WindowDays: WindowDays,
            GeneratedAt: now);
    }
}
