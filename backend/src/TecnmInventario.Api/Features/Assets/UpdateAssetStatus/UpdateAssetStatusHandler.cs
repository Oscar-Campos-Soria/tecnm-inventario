using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Infrastructure;

namespace TecnmInventario.Api.Features.Assets.UpdateAssetStatus;

public sealed class UpdateAssetStatusHandler : ICommandHandler<UpdateAssetStatusCommand, UpdateAssetStatusResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UpdateAssetStatusHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UpdateAssetStatusResponse> Handle(
        UpdateAssetStatusCommand command, CancellationToken cancellationToken)
    {
        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        var newStatus = await _context.AssetStatuses
            .FirstAsync(s => s.Code == command.NewStatusCode && !s.IsDeleted, cancellationToken);

        var previousStatusCode = asset.AssetStatus.Code;

        _context.Entry(asset).Property(a => a.Version).OriginalValue = command.ExpectedVersion;

        asset.AssetStatusId = newStatus.AssetStatusId;
        asset.UpdatedBy = _currentUser.UserId;

        // Transacción explícita: el cambio de estado y su registro en
        // audit_log deben ser atómicos (todo o nada). Sin esto, si el
        // INSERT en audit_log falla por cualquier razón, el UPDATE del
        // activo ya quedaría confirmado en la BD sin dejar rastro de
        // auditoría -- justo lo que esto existe para evitar.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(asset).ReloadAsync(cancellationToken);

        // action debe ser uno de los valores que acepta el CHECK constraint
        // de audit_log (INSERT/UPDATE/DELETE); el detalle real del cambio
        // (a qué estado, por qué) va en new_data, no en el nombre de la acción.
        await _auditLog.LogAsync(
            tableName: "assets",
            recordId: asset.AssetId,
            action: "UPDATE",
            oldData: new { Status = previousStatusCode },
            newData: new { Status = command.NewStatusCode, command.Reason },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new UpdateAssetStatusResponse(asset.AssetId, command.NewStatusCode, asset.Version);
    }
}
