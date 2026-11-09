using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Components.AddComponent;

/// <param name="InstalledAt">Opcional; por defecto, ahora.</param>
public sealed record AddComponentCommand(
    long AssetId, long ComponentTypeId, string? Description, string? SerialNumber, DateTime? InstalledAt)
    : ICommand<ComponentWriteResponse>, ITransactionalCommand;
