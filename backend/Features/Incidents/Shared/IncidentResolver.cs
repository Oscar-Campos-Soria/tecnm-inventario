using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Features.Incidents.ResolveIncident;

namespace TecnmInventario.Api.Features.Incidents.Shared;

/// <summary>
/// Resuelve un incidente y libera el activo según la máquina de estados. Lo usan
/// ResolveIncident y CreateMaintenance (correctivo que resuelve en el mismo paso).
/// Requiere una transacción abierta por el llamador (TransactionBehavior): el
/// incidente, el activo y sus filas de audit_log cambian juntos.
/// </summary>
public sealed class IncidentResolver
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _assetStatusChanger;

    public IncidentResolver(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IAssetStatusChanger assetStatusChanger)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _assetStatusChanger = assetStatusChanger;
    }

    /// <summary>Carga el incidente con lo necesario para resolverlo (estado y activo).</summary>
    public Task<Incident?> LoadAsync(long incidentId, CancellationToken cancellationToken)
        => _context.Incidents
            .Include(i => i.IncidentStatus)
            .Include(i => i.Asset).ThenInclude(a => a.AssetStatus)
            .FirstOrDefaultAsync(i => i.IncidentId == incidentId, cancellationToken);

    /// <summary>El UPDATE compara contra la versión que vio el cliente: si cambió, 409 al guardar.</summary>
    public void ExpectVersion(Incident incident, int expectedVersion)
        => _context.Entry(incident).Property(i => i.Version).OriginalValue = expectedVersion;

    public async Task<ResolveIncidentResponse> ResolveAsync(
        Incident incident, string solution, string? diagnosis, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var previousStatusCode = incident.IncidentStatus.Code;
        if (!IncidentStatusCodes.Open.Contains(previousStatusCode))
        {
            throw new BusinessRuleViolationException(
                "Solo se puede resolver un incidente abierto, en diagnóstico o en reparación.");
        }

        var resolvedStatusId = await _context.IncidentStatuses
            .Where(s => s.Code == IncidentStatusCodes.Resuelto && !s.IsDeleted)
            .Select(s => s.IncidentStatusId)
            .FirstAsync(cancellationToken);

        var trimmedSolution = solution.Trim();
        var trimmedDiagnosis = string.IsNullOrWhiteSpace(diagnosis) ? null : diagnosis.Trim();

        incident.IncidentStatusId = resolvedStatusId;
        incident.Solution = trimmedSolution;
        if (trimmedDiagnosis is not null)
        {
            incident.Diagnosis = trimmedDiagnosis;
        }
        incident.ResolvedAt = DateTime.UtcNow;
        incident.UpdatedBy = userId;

        // Sin transacción propia: la abre TransactionBehavior del comando que llama
        // (resolver un incidente, o registrar el correctivo que lo resuelve).
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(incident).ReloadAsync(cancellationToken);

        var asset = incident.Asset;
        var assetStatusCode = asset.AssetStatus.Code;

        // El activo no se libera mientras quede otro incidente abierto sobre él.
        var openCodes = IncidentStatusCodes.Open.ToArray();
        var otherOpenIncidents = await _context.Incidents.CountAsync(
            i => i.AssetId == incident.AssetId
                 && i.IncidentId != incident.IncidentId
                 && openCodes.Contains(i.IncidentStatus.Code),
            cancellationToken);

        var finalAssetStatus = assetStatusCode;
        var released = false;

        if (otherOpenIncidents == 0
            && (assetStatusCode == AssetStatusCodes.ConFalla
                || assetStatusCode == AssetStatusCodes.EnMantenimiento))
        {
            var hasActiveAssignment = await _context.AssetAssignments
                .AnyAsync(a => a.AssetId == incident.AssetId && a.ReturnedAt == null, cancellationToken);

            var target = hasActiveAssignment ? AssetStatusCodes.Asignado : AssetStatusCodes.Disponible;
            var reason = $"Incidente #{incident.IncidentId} resuelto";

            // La matriz exige pasar por EN_MANTENIMIENTO antes de volver a circular.
            if (assetStatusCode == AssetStatusCodes.ConFalla)
            {
                await _assetStatusChanger.ChangeAsync(
                    asset, AssetStatusCodes.EnMantenimiento, reason + " (paso intermedio)", cancellationToken);
            }

            var change = await _assetStatusChanger.ChangeAsync(asset, target, reason, cancellationToken);
            finalAssetStatus = change.NewStatusCode;
            released = true;
        }
        else if (otherOpenIncidents > 0 && assetStatusCode == AssetStatusCodes.EnMantenimiento)
        {
            // Quedan fallas abiertas pero ninguna en reparación: el activo no está
            // "en mantenimiento", sigue con falla.
            var otherInRepair = await _context.Incidents.AnyAsync(
                i => i.AssetId == incident.AssetId
                     && i.IncidentId != incident.IncidentId
                     && i.IncidentStatus.Code == IncidentStatusCodes.EnReparacion,
                cancellationToken);

            if (!otherInRepair)
            {
                var change = await _assetStatusChanger.ChangeAsync(
                    asset, AssetStatusCodes.ConFalla,
                    $"Incidente #{incident.IncidentId} resuelto; quedan fallas sin reparar", cancellationToken);
                finalAssetStatus = change.NewStatusCode;
            }
        }

        await _auditLog.LogAsync(
            tableName: "incidents",
            recordId: incident.IncidentId,
            action: "UPDATE",
            oldData: new { Status = previousStatusCode },
            newData: new
            {
                Status = IncidentStatusCodes.Resuelto,
                incident.Solution,
                incident.Diagnosis,
                incident.ResolvedAt,
                AssetStatusAfter = finalAssetStatus,
                OtherOpenIncidents = otherOpenIncidents
            },
            cancellationToken);

        return new ResolveIncidentResponse(
            incident.IncidentId,
            IncidentStatusCodes.Resuelto,
            finalAssetStatus,
            released,
            otherOpenIncidents,
            incident.ResolvedAt,
            incident.Version);
    }
}
