using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Components.DeleteComponent;

/// <summary>Borrado lógico de un componente capturado por error (no es un retiro).</summary>
public sealed record DeleteComponentCommand(long AssetId, long AssetComponentId, int ExpectedVersion)
    : ICommand<ComponentWriteResponse>, ITransactionalCommand;
