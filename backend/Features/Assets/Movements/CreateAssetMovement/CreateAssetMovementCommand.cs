using Mediator;

namespace TecnmInventario.Api.Features.Assets.Movements.CreateAssetMovement;

public sealed record CreateAssetMovementCommand(
    long AssetId,
    long ToLocationId,
    long MovementReasonId,
    DateTime? MovedAt,
    string? Notes)
    : ICommand<CreateAssetMovementResponse>;
