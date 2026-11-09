using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Pdf;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Reports.Shared;

namespace TecnmInventario.Api.Features.Reports.GetReport;

/// <summary>
/// Reportes del inventario en Excel o PDF. Solo lectura (no escribe en audit_log).
/// Cada reporte arma una ReportTable y ReportRenderer la convierte al formato pedido.
/// </summary>
public sealed class GetReportHandler : IQueryHandler<GetReportQuery, GeneratedFile>
{
    private const int Take = ReportCodes.MaxRows + 1;
    private readonly TecnmInventarioDbContext _context;

    public GetReportHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GeneratedFile> Handle(GetReportQuery query, CancellationToken cancellationToken)
    {
        var report = query.Report switch
        {
            ReportCodes.AssetsByLocation => await AssetsByLocation(cancellationToken),
            ReportCodes.Assets => await Assets(query, cancellationToken),
            ReportCodes.OpenIncidents => await OpenIncidents(cancellationToken),
            ReportCodes.Maintenance => await Maintenance(query, cancellationToken),
            _ => await Licenses(cancellationToken)
        };
        return ReportRenderer.Render(report, query.Format, DateTime.UtcNow);
    }

    private static ReportTable Table(string title, string slug, IReadOnlyList<string> filters, string[] columns, List<object?[]> rows)
    {
        var truncated = rows.Count > ReportCodes.MaxRows;
        return new ReportTable(title, slug, filters, columns, truncated ? rows.Take(ReportCodes.MaxRows).ToList() : rows, truncated);
    }

    private async Task<ReportTable> AssetsByLocation(CancellationToken ct)
    {
        var rows = await _context.Assets.AsNoTracking()
            .Where(a => !a.IsDeleted && a.AssetStatus.Code != AssetStatusCodes.DadoDeBaja)
            .OrderBy(a => a.CurrentLocation == null).ThenBy(a => a.CurrentLocation!.Code).ThenBy(a => a.InventoryCode)
            .Take(Take)
            .Select(a => new object?[]
            {
                a.CurrentLocation != null ? a.CurrentLocation.Code : "Sin ubicación",
                a.CurrentLocation != null ? a.CurrentLocation.Name : null,
                a.InventoryCode, a.AssetType.Name, a.AssetStatus.Name, a.SerialNumber, a.Hostname
            })
            .ToListAsync(ct);
        return Table("Equipos por ubicación", "equipos-por-ubicacion", new[] { "Equipos en servicio (sin dados de baja)" },
            new[] { "Código ubicación", "Ubicación", "Código", "Tipo", "Estado", "Serie", "Hostname" }, rows);
    }

    private async Task<ReportTable> Assets(GetReportQuery q, CancellationToken ct)
    {
        var assets = _context.Assets.AsNoTracking().Where(a => !a.IsDeleted);
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            assets = assets.Where(a => a.AssetStatus.Code == q.Status);
            filters.Add($"Estado: {q.Status}");
        }
        else
        {
            assets = assets.Where(a => a.AssetStatus.Code != AssetStatusCodes.DadoDeBaja);
            filters.Add("Sin dados de baja");
        }
        if (q.AssetTypeId is long typeId)
        {
            assets = assets.Where(a => a.AssetTypeId == typeId);
            filters.Add($"Tipo: {await _context.AssetTypes.Where(t => t.AssetTypeId == typeId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? "?"}");
        }

        var rows = await assets
            .OrderBy(a => a.InventoryCode)
            .Take(Take)
            .Select(a => new object?[]
            {
                a.InventoryCode, a.AssetType.Name, a.AssetStatus.Name,
                a.Manufacturer != null ? a.Manufacturer.Name : null,
                a.Model != null ? a.Model.ModelName : null,
                a.SerialNumber, a.Hostname,
                a.CurrentLocation != null ? a.CurrentLocation.Name : null,
                a.AssetAssignments.Where(x => x.ReturnedAt == null)
                    .Select(x => x.AssignedToNavigation.FirstName + " " + x.AssignedToNavigation.LastName).FirstOrDefault(),
                a.CreatedAt
            })
            .ToListAsync(ct);
        return Table("Resumen de activos", "activos", filters,
            new[] { "Código", "Tipo", "Estado", "Marca", "Modelo", "Serie", "Hostname", "Ubicación", "Resguardante", "Alta" }, rows);
    }

    private async Task<ReportTable> OpenIncidents(CancellationToken ct)
    {
        var open = IncidentStatusCodes.Open.ToArray();
        var now = DateTime.UtcNow;
        var data = await _context.Incidents.AsNoTracking()
            .Where(i => open.Contains(i.IncidentStatus.Code))
            .OrderByDescending(i => i.IncidentPriorityId).ThenBy(i => i.ReportedAt)
            .Take(Take)
            .Select(i => new
            {
                i.IncidentId, i.Asset.InventoryCode, i.Title,
                Priority = i.IncidentPriority.Name, Status = i.IncidentStatus.Name,
                Reporter = i.ReportedByNavigation.FirstName + " " + i.ReportedByNavigation.LastName,
                Technician = i.AssignedToNavigation != null ? i.AssignedToNavigation.FirstName + " " + i.AssignedToNavigation.LastName : null,
                i.ReportedAt
            })
            .ToListAsync(ct);
        var rows = data.Select(i => new object?[]
        {
            i.IncidentId, i.InventoryCode, i.Title, i.Priority, i.Status, i.Reporter, i.Technician, i.ReportedAt,
            (int)(now - i.ReportedAt).TotalDays
        }).ToList();
        return Table("Incidentes abiertos", "incidentes-abiertos", Array.Empty<string>(),
            new[] { "Folio", "Activo", "Título", "Prioridad", "Estado", "Reportó", "Técnico", "Reportado", "Días abierto" }, rows);
    }

    private async Task<ReportTable> Maintenance(GetReportQuery q, CancellationToken ct)
    {
        var records = _context.MaintenanceRecords.AsNoTracking().Where(r => !r.IsDeleted && r.PerformedAt != null);
        var filters = new List<string>();
        if (q.From is DateOnly from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            records = records.Where(r => r.PerformedAt >= start);
            filters.Add($"Desde {from:dd/MM/yyyy}");
        }
        if (q.To is DateOnly to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            records = records.Where(r => r.PerformedAt < end);
            filters.Add($"Hasta {to:dd/MM/yyyy}");
        }

        var rows = await records
            .OrderByDescending(r => r.PerformedAt)
            .Take(Take)
            .Select(r => new object?[]
            {
                r.PerformedAt, r.Asset.InventoryCode, r.MaintenanceType.Name, r.Description, r.PartsUsed, r.Cost,
                r.PerformedByNavigation != null ? r.PerformedByNavigation.FirstName + " " + r.PerformedByNavigation.LastName : null,
                r.IncidentId, r.NextMaintenanceAt
            })
            .ToListAsync(ct);
        return Table("Mantenimientos realizados", "mantenimientos", filters,
            new[] { "Fecha", "Activo", "Tipo", "Descripción", "Refacciones", "Costo MXN", "Técnico", "Incidente", "Próximo" }, rows);
    }

    // Nunca incluye la clave de licencia.
    private async Task<ReportTable> Licenses(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var data = await _context.SoftwareLicenses.AsNoTracking()
            .OrderBy(l => l.ValidUntil == null).ThenBy(l => l.ValidUntil).ThenBy(l => l.Software.Name)
            .Take(Take)
            .Select(l => new
            {
                l.Software.Name, l.Software.Vendor, Type = l.LicenseType.Name, l.QuantityPurchased,
                InUse = l.SoftwareLicenseAssignments.Count(a => a.EndedAt == null),
                l.ValidFrom, l.ValidUntil, Supplier = l.Supplier != null ? l.Supplier.Name : null
            })
            .ToListAsync(ct);
        var rows = data.Select(l => new object?[]
        {
            l.Name, l.Vendor, l.Type, l.InUse, l.QuantityPurchased?.ToString() ?? "Sin límite",
            l.QuantityPurchased is int q ? q - l.InUse : null, l.ValidFrom, l.ValidUntil,
            l.ValidUntil is null ? "Sin vencimiento" : l.ValidUntil < today ? "Vencida" : "Vigente",
            l.Supplier
        }).ToList();
        return Table("Licencias de software", "licencias", new[] { "Sin claves de licencia" },
            new[] { "Programa", "Fabricante", "Tipo", "En uso", "Permitidos", "Disponibles", "Desde", "Vence", "Estado", "Proveedor" }, rows);
    }
}
