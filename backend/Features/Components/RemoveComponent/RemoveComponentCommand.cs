using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Components.RemoveComponent;

/// <param name="RemovedAt">Opcional; por defecto, ahora.</param>
public sealed record RemoveComponentCommand(long AssetId, long AssetComponentId, int ExpectedVersion, DateTime? RemovedAt)
    : ICommand<ComponentWriteResponse>, ITransactionalCommand;
