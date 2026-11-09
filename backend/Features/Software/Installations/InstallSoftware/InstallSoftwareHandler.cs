using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Installations.Shared;

namespace TecnmInventario.Api.Features.Software.Installations.InstallSoftware;

public sealed class InstallSoftwareHandler : ICommandHandler<InstallSoftwareCommand, InstallSoftwareResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public InstallSoftwareHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<InstallSoftwareResponse> Handle(
        InstallSoftwareCommand command, CancellationToken cancellationToken)
    {
        await InstallationRules.LockAssetAsync(_context, command.AssetId, cancellationToken);

        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El activo solicitado no existe.");

        if (asset.AssetStatus.Code == AssetStatusCodes.DadoDeBaja)
        {
            throw new BusinessRuleViolationException("No se puede instalar software en un activo dado de baja.");
        }

        var version = string.IsNullOrWhiteSpace(command.VersionInstalled) ? null : command.VersionInstalled.Trim();

        // Instalar otra versión del mismo programa la reemplaza: la anterior queda como historial.
        var previous = await _context.AssetSoftwares.FirstOrDefaultAsync(
            i => i.AssetId == asset.AssetId && i.SoftwareId == command.SoftwareId && i.IsCurrent && !i.IsDeleted,
            cancellationToken);

        if (previous is not null && previous.VersionInstalled == version)
        {
            throw new BusinessRuleViolationException("El programa ya está instalado en esa versión en este equipo.");
        }

        if (previous is not null)
        {
            var before = InstallationRules.Snapshot(previous);
            previous.IsCurrent = false;
            previous.UpdatedBy = _currentUser.UserId;

            // UpdatedAt y Version los actualiza el trigger touch_version.
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Entry(previous).ReloadAsync(cancellationToken);

            await _auditLog.LogAsync(
                tableName: "asset_software",
                recordId: previous.AssetSoftwareId,
                action: "UPDATE",
                oldData: before,
                newData: InstallationRules.Snapshot(previous),
                cancellationToken);
        }

        var now = DateTime.UtcNow;
        var install = new AssetSoftware
        {
            AssetId = asset.AssetId,
            SoftwareId = command.SoftwareId,
            VersionInstalled = version,
            InstalledAt = command.InstalledAt?.ToUniversalTime() ?? now,
            IsCurrent = true,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        _context.AssetSoftwares.Add(install);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "asset_software",
            recordId: install.AssetSoftwareId,
            action: "INSERT",
            oldData: null,
            newData: InstallationRules.Snapshot(install),
            cancellationToken);

        return new InstallSoftwareResponse(
            install.AssetSoftwareId, install.AssetId, install.SoftwareId, install.VersionInstalled,
            previous?.AssetSoftwareId, install.Version);
    }
}
