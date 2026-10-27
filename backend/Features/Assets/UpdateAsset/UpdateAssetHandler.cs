using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
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
        // Asset no tiene query filter global (Lote 4): el filtro por IsDeleted
        // se hace explícito para no editar registros ya dados de baja lógica.
        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(
                a => a.AssetId == command.AssetId && !a.IsDeleted,
                cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        if (asset.AssetStatus.Code == AssetStatusCodes.DadoDeBaja)
        {
            throw new BusinessRuleViolationException(
                "El activo está dado de baja. Ese estado es terminal y el activo ya no puede editarse.");
        }

        _context.Entry(asset).Property(a => a.Version).OriginalValue = command.ExpectedVersion;

        // TODO(lote-2): abrir transacción explícita y registrar el cambio en
        // audit_log. El snapshot "antes" debe capturarse antes de mutar la
        // entidad (en este punto, justo después del control de estado).
        // La ubicación NO se edita aquí: solo cambia con asset_movements.
        asset.ManufacturerId = command.ManufacturerId;
        asset.ModelId = command.ModelId;
        asset.SerialNumber = string.IsNullOrWhiteSpace(command.SerialNumber) ? null : command.SerialNumber.Trim();
        asset.Hostname = string.IsNullOrWhiteSpace(command.Hostname) ? null : command.Hostname.Trim();
        asset.Notes = command.Notes;
        asset.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(asset).ReloadAsync(cancellationToken);

        return new UpdateAssetResponse(asset.AssetId, asset.Version);
    }
}
