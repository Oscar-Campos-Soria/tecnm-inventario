using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Incidents.Shared;

namespace TecnmInventario.Api.Features.Maintenance.CreateMaintenance;

/// <summary>
/// Bitácora de mantenimiento: NO cambia el estado del activo (eso lo
/// controlan los incidentes). Un correctivo puede ligarse a su incidente.
/// </summary>
public sealed class CreateMaintenanceHandler : ICommandHandler<CreateMaintenanceCommand, CreateMaintenanceResponse>
{
    private const string ManagePermission = "maintenance.manage";

    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IRolePermissionService _rolePermissions;
    private readonly IncidentResolver _incidentResolver;

    public CreateMaintenanceHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IRolePermissionService rolePermissions,
        IncidentResolver incidentResolver)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _rolePermissions = rolePermissions;
        _incidentResolver = incidentResolver;
    }

    public async ValueTask<CreateMaintenanceResponse> Handle(
        CreateMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var assetStatus = await _context.Assets
            .Where(a => a.AssetId == command.AssetId && !a.IsDeleted)
            .Select(a => a.AssetStatus.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("El activo solicitado no existe.");

        if (assetStatus == AssetStatusCodes.DadoDeBaja)
            throw new BusinessRuleViolationException("No se registra mantenimiento a un activo dado de baja.");

        var typeCode = await _context.MaintenanceTypes
            .Where(t => t.MaintenanceTypeId == command.MaintenanceTypeId && !t.IsDeleted)
            .Select(t => t.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleViolationException("El tipo de mantenimiento especificado no existe.");

        if (command.ResolveIncident && typeCode != MaintenanceTypeCodes.Correctivo)
            throw new BusinessRuleViolationException("Solo un mantenimiento correctivo puede resolver un incidente.");

        if (command.IncidentId.HasValue)
        {
            var incidentAssetId = await _context.Incidents
                .Where(i => i.IncidentId == command.IncidentId.Value)
                .Select(i => (long?)i.AssetId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleViolationException("El incidente especificado no existe.");

            if (incidentAssetId != command.AssetId)
                throw new BusinessRuleViolationException("El incidente pertenece a otro activo.");
        }

        var performedBy = command.PerformedBy ?? userId;
        if (performedBy != userId)
        {
            // Mensaje único para "no existe", "inactivo" o "sin permiso".
            var roleCode = await _context.AppUsers
                .Where(u => u.UserId == performedBy && !u.IsDeleted && u.Status == "activo")
                .Select(u => u.Role.Code)
                .FirstOrDefaultAsync(cancellationToken);
            if (roleCode is null || !await _rolePermissions.HasPermissionAsync(roleCode, ManagePermission, cancellationToken))
                throw new BusinessRuleViolationException("El técnico indicado no existe, está inactivo o no da mantenimiento.");
        }

        // La transacción la abre TransactionBehavior: el registro, el incidente
        // resuelto y el activo liberado se confirman juntos o nada.
        var record = new MaintenanceRecord
        {
            AssetId = command.AssetId,
            MaintenanceTypeId = command.MaintenanceTypeId,
            PerformedBy = performedBy,
            PerformedAt = command.PerformedAt.ToUniversalTime(),
            NextMaintenanceAt = command.NextMaintenanceAt?.ToUniversalTime(),
            Description = command.Description.Trim(),
            PartsUsed = string.IsNullOrWhiteSpace(command.PartsUsed) ? null : command.PartsUsed.Trim(),
            Cost = command.Cost,
            IncidentId = command.IncidentId,
            CreatedBy = userId,
            UpdatedBy = userId,
            Version = 1
        };
        _context.MaintenanceRecords.Add(record);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "maintenance_records",
            recordId: record.MaintenanceRecordId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                record.MaintenanceRecordId,
                record.AssetId,
                Type = typeCode,
                record.PerformedBy,
                record.PerformedAt,
                record.NextMaintenanceAt,
                record.Cost,
                record.IncidentId
            },
            cancellationToken);

        string? assetStatusAfter = null;
        if (command.ResolveIncident)
        {
            var incident = await _incidentResolver.LoadAsync(command.IncidentId!.Value, cancellationToken)
                ?? throw new KeyNotFoundException("El incidente especificado no existe.");
            var solution = string.IsNullOrWhiteSpace(command.IncidentSolution) ? record.Description! : command.IncidentSolution;
            var resolved = await _incidentResolver.ResolveAsync(incident, solution, null, cancellationToken);
            assetStatusAfter = resolved.AssetStatusCode;
        }

        return new CreateMaintenanceResponse(
            record.MaintenanceRecordId, record.AssetId, typeCode, record.PerformedAt!.Value,
            record.NextMaintenanceAt, record.Version, assetStatusAfter);
    }
}
