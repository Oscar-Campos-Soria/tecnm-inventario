using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.Movements.CreateAssetMovement;

public sealed class CreateAssetMovementHandler
    : ICommandHandler<CreateAssetMovementCommand, CreateAssetMovementResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateAssetMovementHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<CreateAssetMovementResponse> Handle(
        CreateAssetMovementCommand command, CancellationToken cancellationToken)
    {
        var asset = await _context.Assets
            .FirstOrDefaultAsync(
                a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        if (asset.CurrentLocationId == command.ToLocationId)
        {
            throw new BusinessRuleViolationException(
                "El destino coincide con la ubicación actual del activo.");
        }

        var now = DateTime.UtcNow;

        // El movimiento es el historial: registra origen (ubicación actual
        // desnormalizada) y destino, y actualiza la ubicación del activo
        // en la misma transacción.
        var movement = new AssetMovement
        {
            AssetId = asset.AssetId,
            FromLocationId = asset.CurrentLocationId,
            ToLocationId = command.ToLocationId,
            MovementReasonId = command.MovementReasonId,
            MovedBy = _currentUser.UserId
                ?? throw new ForbiddenAccessException("Se requiere usuario autenticado."),
            MovedAt = command.MovedAt ?? now,
            Notes = command.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        asset.CurrentLocationId = command.ToLocationId;
        asset.UpdatedAt = now;

        _context.AssetMovements.Add(movement);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateAssetMovementResponse(
            movement.AssetMovementId,
            movement.AssetId,
            movement.FromLocationId,
            movement.ToLocationId,
            movement.MovedAt);
    }
}
