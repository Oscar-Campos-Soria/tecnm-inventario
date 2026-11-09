using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Documents.GetAssetDocuments;

/// <summary>
/// Archivos de todos los registros de un activo (incidentes, mantenimientos,
/// baja, compras y tickets). Cada dueño pasa por IDocumentAccessAuthorizer:
/// los que el usuario no puede ver se omiten en silencio, con las mismas
/// reglas que el listado por dueño.
/// </summary>
public sealed class GetAssetDocumentsHandler
    : IQueryHandler<GetAssetDocumentsQuery, IReadOnlyList<GetAssetDocumentsResponse>>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IDocumentAccessAuthorizer _accessAuthorizer;

    public GetAssetDocumentsHandler(TecnmInventarioDbContext context, IDocumentAccessAuthorizer accessAuthorizer)
    {
        _context = context;
        _accessAuthorizer = accessAuthorizer;
    }

    public async ValueTask<IReadOnlyList<GetAssetDocumentsResponse>> Handle(
        GetAssetDocumentsQuery query, CancellationToken cancellationToken)
    {
        var assetId = query.AssetId;
        if (!await _context.Assets.AnyAsync(a => a.AssetId == assetId && !a.IsDeleted, cancellationToken))
            throw new KeyNotFoundException("El activo solicitado no existe.");

        // Dueños del activo con su etiqueta legible. Por equipo son pocas filas.
        var owners = new Dictionary<(string Type, long Id), string>();
        foreach (var i in await _context.Incidents.AsNoTracking().Where(i => i.AssetId == assetId)
                     .Select(i => new { i.IncidentId, i.Title }).ToListAsync(cancellationToken))
            owners[(DocumentOwnerTypes.Incident, i.IncidentId)] = $"Incidente #{i.IncidentId}: {i.Title}";
        foreach (var m in await _context.MaintenanceRecords.AsNoTracking().Where(m => m.AssetId == assetId)
                     .Select(m => new { m.MaintenanceRecordId, Type = m.MaintenanceType.Name }).ToListAsync(cancellationToken))
            owners[(DocumentOwnerTypes.Maintenance, m.MaintenanceRecordId)] = $"{m.Type} #{m.MaintenanceRecordId}";
        foreach (var d in await _context.AssetDisposals.AsNoTracking().Where(d => d.AssetId == assetId)
                     .Select(d => new { d.AssetDisposalId, d.Folio }).ToListAsync(cancellationToken))
            owners[(DocumentOwnerTypes.AssetDisposal, d.AssetDisposalId)] = $"Baja {d.Folio}";
        foreach (var a in await _context.AcquisitionAssets.AsNoTracking().Where(x => x.AssetId == assetId)
                     .Join(_context.Acquisitions, x => x.AcquisitionId, a => a.AcquisitionId,
                         (x, a) => new { a.AcquisitionId, a.PurchaseOrder })
                     .ToListAsync(cancellationToken))
            owners[(DocumentOwnerTypes.Acquisition, a.AcquisitionId)] =
                a.PurchaseOrder is null ? $"Compra #{a.AcquisitionId}" : $"Compra OC {a.PurchaseOrder}";
        foreach (var t in await _context.Tickets.AsNoTracking().Where(t => t.AssetId == assetId)
                     .Select(t => new { t.TicketId, t.Title }).ToListAsync(cancellationToken))
            owners[(DocumentOwnerTypes.Ticket, t.TicketId)] = $"Ticket #{t.TicketId}: {t.Title}";

        if (owners.Count == 0)
            return Array.Empty<GetAssetDocumentsResponse>();

        var ownerIds = owners.Keys.Select(k => k.Id).Distinct().ToList();
        var candidates = await _context.Documents.AsNoTracking()
            .Where(d => !d.IsDeleted && ownerIds.Contains(d.OwnerId))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var allowed = new Dictionary<(string, long), bool>();
        var result = new List<GetAssetDocumentsResponse>();
        foreach (var d in candidates)
        {
            var key = (d.OwnerType, d.OwnerId);
            if (!owners.TryGetValue(key, out var label))
                continue; // mismo id, otro tipo de dueño

            if (!allowed.TryGetValue(key, out var canRead))
                allowed[key] = canRead = await CanReadAsync(d.OwnerType, d.OwnerId, cancellationToken);
            if (!canRead)
                continue;

            result.Add(new GetAssetDocumentsResponse(
                d.DocumentId, d.FileName, d.MimeType, d.SizeBytes, d.Description, d.CreatedAt,
                d.OwnerType, d.OwnerId, label));
        }

        return result;
    }

    private async Task<bool> CanReadAsync(string ownerType, long ownerId, CancellationToken cancellationToken)
    {
        try
        {
            await _accessAuthorizer.EnsureAuthorizedAsync(ownerType, ownerId, DocumentOperation.Read, null, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is ForbiddenAccessException or KeyNotFoundException)
        {
            return false;
        }
    }
}
