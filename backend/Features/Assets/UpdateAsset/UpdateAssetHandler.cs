using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.UpdateAsset;

public sealed class UpdateAssetHandler : ICommandHandler<UpdateAssetCommand, UpdateAssetResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateAssetHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<UpdateAssetResponse> Handle(
        UpdateAssetCommand command, CancellationToken cancellationToken)
    {
        var asset = await _context.Assets
            .FirstOrDefaultAsync(a => a.AssetId == command.AssetId, cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        _context.Entry(asset).Property(a => a.Version).OriginalValue = command.ExpectedVersion;

        asset.ManufacturerId = command.ManufacturerId;
        asset.ModelId = command.ModelId;
        asset.CurrentLocationId = command.CurrentLocationId;
        asset.SerialNumber = string.IsNullOrWhiteSpace(command.SerialNumber) ? null : command.SerialNumber.Trim();
        asset.Hostname = string.IsNullOrWhiteSpace(command.Hostname) ? null : command.Hostname.Trim();
        asset.Notes = command.Notes;
        asset.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(asset).ReloadAsync(cancellationToken);

        return new UpdateAssetResponse(asset.AssetId, asset.Version);
    }
}
