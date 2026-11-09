using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Components.Shared;

namespace TecnmInventario.Api.Features.Components.ReplaceComponent;

/// <summary>
/// Reemplazo: el componente actual queda retirado y se crea el nuevo del mismo
/// tipo, en la misma transacción (historial completo del activo).
/// </summary>
public sealed class ReplaceComponentHandler : ICommandHandler<ReplaceComponentCommand, ComponentWriteResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public ReplaceComponentHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<ComponentWriteResponse> Handle(
        ReplaceComponentCommand command, CancellationToken cancellationToken)
    {
        await ComponentRules.EnsureAssetInServiceAsync(_context, command.AssetId, cancellationToken);
        var old = await ComponentRules.LoadAsync(
            _context, command.AssetId, command.AssetComponentId, command.ExpectedVersion, cancellationToken);

        if (old.RemovedAt is not null)
        {
            throw new BusinessRuleViolationException("Ese componente ya fue retirado; agrega uno nuevo en su lugar.");
        }

        var now = DateTime.UtcNow;
        var replacedAt = command.ReplacedAt?.ToUniversalTime() ?? now;
        if (replacedAt < old.InstalledAt)
        {
            throw new BusinessRuleViolationException("La fecha del cambio no puede ser anterior a la instalación del componente actual.");
        }

        var before = ComponentRules.Snapshot(old);
        _context.Entry(old).Property(c => c.Version).OriginalValue = command.ExpectedVersion;
        old.RemovedAt = replacedAt;
        old.UpdatedBy = _currentUser.UserId;

        // UpdatedAt y Version los actualiza el trigger touch_version.
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(old).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "asset_components",
            recordId: old.AssetComponentId,
            action: "UPDATE",
            oldData: before,
            newData: ComponentRules.Snapshot(old),
            cancellationToken);

        var replacement = new AssetComponent
        {
            AssetId = old.AssetId,
            ComponentTypeId = old.ComponentTypeId,
            Description = ComponentRules.NullIfBlank(command.Description),
            SerialNumber = ComponentRules.NullIfBlank(command.SerialNumber),
            InstalledAt = replacedAt,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        _context.AssetComponents.Add(replacement);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "asset_components",
            recordId: replacement.AssetComponentId,
            action: "INSERT",
            oldData: null,
            newData: ComponentRules.Snapshot(replacement),
            cancellationToken);

        return new ComponentWriteResponse(replacement.AssetComponentId, replacement.Version, old.AssetComponentId);
    }
}
