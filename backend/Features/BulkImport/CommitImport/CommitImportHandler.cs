using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Features.BulkImport.CommitImport;

/// <summary>
/// Da de alta todos los activos del archivo en una sola transacción (TransactionBehavior).
/// Se vuelve a validar todo (el archivo pudo cambiar o alguien tomó un código): si una
/// sola fila tiene error no se crea ninguna, para no dejar cargas a medias.
/// </summary>
public sealed class CommitImportHandler : ICommandHandler<CommitImportCommand, CommitImportResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly AssetImportChecker _checker;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CommitImportHandler(
        TecnmInventarioDbContext context, AssetImportChecker checker,
        ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _checker = checker;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CommitImportResponse> Handle(CommitImportCommand command, CancellationToken cancellationToken)
    {
        var rows = await _checker.CheckAsync(AssetImportParser.Parse(command.File), cancellationToken);
        var invalid = rows.Count(r => !r.IsValid);
        if (invalid > 0)
        {
            throw new BusinessRuleViolationException(
                $"El archivo tiene {invalid} fila(s) con errores. Corrígelas y vuelve a revisar el archivo; no se creó ningún activo.");
        }

        var status = await _context.AssetStatuses
            .FirstAsync(s => s.Code == AssetStatusCodes.Disponible && !s.IsDeleted, cancellationToken);
        var now = DateTime.UtcNow;
        var assets = rows.Select(r => new Asset
        {
            InventoryCode = r.InventoryCode!,
            AssetTypeId = r.AssetTypeId,
            AssetStatusId = status.AssetStatusId,
            ManufacturerId = r.ManufacturerId,
            ModelId = r.ModelId,
            CurrentLocationId = r.LocationId,
            SerialNumber = r.SerialNumber,
            Hostname = r.Hostname,
            Notes = r.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        }).ToList();

        _context.Assets.AddRange(assets);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var a in assets)
        {
            await _auditLog.LogAsync(
                tableName: "assets",
                recordId: a.AssetId,
                action: "INSERT",
                oldData: null,
                newData: new
                {
                    a.InventoryCode, a.AssetTypeId, a.AssetStatusId, a.ManufacturerId, a.ModelId,
                    a.CurrentLocationId, a.SerialNumber, a.Hostname, a.Notes, a.Version,
                    Source = "CARGA_MASIVA", File = command.File.FileName
                },
                cancellationToken);
        }

        return new CommitImportResponse(assets.Count, assets.Select(a => a.AssetId).ToList());
    }
}
