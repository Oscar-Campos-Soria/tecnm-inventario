using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Components.ReplaceComponent;

/// <param name="ReplacedAt">Fecha del cambio: retiro del anterior e instalación del nuevo. Por defecto, ahora.</param>
public sealed record ReplaceComponentCommand(
    long AssetId, long AssetComponentId, int ExpectedVersion,
    string? Description, string? SerialNumber, DateTime? ReplacedAt)
    : ICommand<ComponentWriteResponse>, ITransactionalCommand;
