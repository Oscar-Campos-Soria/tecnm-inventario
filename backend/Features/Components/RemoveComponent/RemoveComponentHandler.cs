using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Components.Shared;

namespace TecnmInventario.Api.Features.Components.RemoveComponent;

public sealed class RemoveComponentHandler : ICommandHandler<RemoveComponentCommand, ComponentWriteResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public RemoveComponentHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<ComponentWriteResponse> Handle(
        RemoveComponentCommand command, CancellationToken cancellationToken)
    {
        await ComponentRules.EnsureAssetInServiceAsync(_context, command.AssetId, cancellationToken);
        var component = await ComponentRules.LoadAsync(
            _context, command.AssetId, command.AssetComponentId, command.ExpectedVersion, cancellationToken);

        if (component.RemovedAt is not null)
        {
            throw new BusinessRuleViolationException("Ese componente ya fue retirado.");
        }

        var removedAt = command.RemovedAt?.ToUniversalTime() ?? DateTime.UtcNow;
        if (removedAt < component.InstalledAt)
        {
            throw new BusinessRuleViolationException("La fecha de retiro no puede ser anterior a la instalación.");
        }

        var before = ComponentRules.Snapshot(component);
        _context.Entry(component).Property(c => c.Version).OriginalValue = command.ExpectedVersion;
        component.RemovedAt = removedAt;
        component.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(component).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "asset_components",
            recordId: component.AssetComponentId,
            action: "UPDATE",
            oldData: before,
            newData: ComponentRules.Snapshot(component),
            cancellationToken);

        return new ComponentWriteResponse(component.AssetComponentId, component.Version);
    }
}
