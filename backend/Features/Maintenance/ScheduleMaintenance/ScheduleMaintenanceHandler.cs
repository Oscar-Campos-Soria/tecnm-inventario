using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;

/// <summary>
/// Programa un mantenimiento a futuro: queda con scheduled_at y sin performed_at
/// hasta que se marca como realizado. No cambia el estado del activo.
/// </summary>
public sealed class ScheduleMaintenanceHandler : ICommandHandler<ScheduleMaintenanceCommand, ScheduleMaintenanceResponse>
{
    private const string ManagePermission = "maintenance.manage";

    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IRolePermissionService _rolePermissions;

    public ScheduleMaintenanceHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser,
        IAuditLogService auditLog, IRolePermissionService rolePermissions)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _rolePermissions = rolePermissions;
    }

    public async ValueTask<ScheduleMaintenanceResponse> Handle(
        ScheduleMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var assetStatus = await _context.Assets
            .Where(a => a.AssetId == command.AssetId && !a.IsDeleted)
            .Select(a => a.AssetStatus.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("El activo solicitado no existe.");
        if (assetStatus == AssetStatusCodes.DadoDeBaja)
            throw new BusinessRuleViolationException("No se programa mantenimiento a un activo dado de baja.");

        var typeCode = await _context.MaintenanceTypes
            .Where(t => t.MaintenanceTypeId == command.MaintenanceTypeId && !t.IsDeleted)
            .Select(t => t.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleViolationException("El tipo de mantenimiento especificado no existe.");

        if (command.PerformedBy is long assignee)
        {
            var roleCode = await _context.AppUsers
                .Where(u => u.UserId == assignee && !u.IsDeleted && u.Status == "activo")
                .Select(u => u.Role.Code)
                .FirstOrDefaultAsync(cancellationToken);
            if (roleCode is null || !await _rolePermissions.HasPermissionAsync(roleCode, ManagePermission, cancellationToken))
                throw new BusinessRuleViolationException("El técnico indicado no existe, está inactivo o no da mantenimiento.");
        }

        var record = new MaintenanceRecord
        {
            AssetId = command.AssetId,
            MaintenanceTypeId = command.MaintenanceTypeId,
            ScheduledAt = command.ScheduledAt.ToUniversalTime(),
            PerformedBy = command.PerformedBy,
            Description = command.Description.Trim(),
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
            newData: new { record.MaintenanceRecordId, record.AssetId, Type = typeCode, record.ScheduledAt, record.PerformedBy, record.Version },
            cancellationToken);

        return new ScheduleMaintenanceResponse(record.MaintenanceRecordId, record.ScheduledAt!.Value, record.Version);
    }
}
