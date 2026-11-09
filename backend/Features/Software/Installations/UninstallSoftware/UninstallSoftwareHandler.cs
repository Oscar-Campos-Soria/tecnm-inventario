using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Installations.Shared;

namespace TecnmInventario.Api.Features.Software.Installations.UninstallSoftware;

public sealed class UninstallSoftwareHandler : ICommandHandler<UninstallSoftwareCommand, UninstallSoftwareResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UninstallSoftwareHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UninstallSoftwareResponse> Handle(
        UninstallSoftwareCommand command, CancellationToken cancellationToken)
    {
        var install = await _context.AssetSoftwares.FirstOrDefaultAsync(
                i => i.AssetSoftwareId == command.AssetSoftwareId && i.AssetId == command.AssetId && !i.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("La instalación solicitada no existe en este equipo.");

        if (install.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException("La instalación fue modificada por otro usuario mientras tanto.");
        }

        if (!install.IsCurrent)
        {
            throw new BusinessRuleViolationException("El programa ya no está instalado en este equipo.");
        }

        var before = InstallationRules.Snapshot(install);

        _context.Entry(install).Property(i => i.Version).OriginalValue = command.ExpectedVersion;
        install.IsCurrent = false;
        install.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(install).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "asset_software",
            recordId: install.AssetSoftwareId,
            action: "UPDATE",
            oldData: before,
            newData: InstallationRules.Snapshot(install),
            cancellationToken);

        // No se libera la licencia en automático: puede reinstalarse; se avisa para que la UI lo ofrezca.
        var hasActiveLicense = await _context.SoftwareLicenseAssignments.AnyAsync(
            a => a.AssetId == install.AssetId && a.EndedAt == null && a.License.SoftwareId == install.SoftwareId,
            cancellationToken);

        return new UninstallSoftwareResponse(install.AssetSoftwareId, hasActiveLicense, install.Version);
    }
}
