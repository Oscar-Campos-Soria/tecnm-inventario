using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Components.Shared;

namespace TecnmInventario.Api.Features.Components.AddComponent;

public sealed class AddComponentHandler : ICommandHandler<AddComponentCommand, ComponentWriteResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public AddComponentHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<ComponentWriteResponse> Handle(AddComponentCommand command, CancellationToken cancellationToken)
    {
        await ComponentRules.EnsureAssetInServiceAsync(_context, command.AssetId, cancellationToken);

        var now = DateTime.UtcNow;
        var component = new AssetComponent
        {
            AssetId = command.AssetId,
            ComponentTypeId = command.ComponentTypeId,
            Description = ComponentRules.NullIfBlank(command.Description),
            SerialNumber = ComponentRules.NullIfBlank(command.SerialNumber),
            InstalledAt = command.InstalledAt?.ToUniversalTime() ?? now,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        _context.AssetComponents.Add(component);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "asset_components",
            recordId: component.AssetComponentId,
            action: "INSERT",
            oldData: null,
            newData: ComponentRules.Snapshot(component),
            cancellationToken);

        return new ComponentWriteResponse(component.AssetComponentId, component.Version);
    }
}
