using Mediator;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Components.Shared;

namespace TecnmInventario.Api.Features.Components.DeleteComponent;

public sealed class DeleteComponentHandler : ICommandHandler<DeleteComponentCommand, ComponentWriteResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteComponentHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<ComponentWriteResponse> Handle(
        DeleteComponentCommand command, CancellationToken cancellationToken)
    {
        var component = await ComponentRules.LoadAsync(
            _context, command.AssetId, command.AssetComponentId, command.ExpectedVersion, cancellationToken);

        await _softDeleteAuditor.DeleteAsync(
            component,
            recordId: component.AssetComponentId,
            expectedVersion: command.ExpectedVersion,
            tableName: "asset_components",
            snapshotBeforeDelete: ComponentRules.Snapshot(component),
            cancellationToken);

        return new ComponentWriteResponse(component.AssetComponentId, component.Version);
    }
}
