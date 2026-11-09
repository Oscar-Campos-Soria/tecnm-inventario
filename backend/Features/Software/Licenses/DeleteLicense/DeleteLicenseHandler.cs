using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Licenses.Shared;

namespace TecnmInventario.Api.Features.Software.Licenses.DeleteLicense;

public sealed class DeleteLicenseHandler : ICommandHandler<DeleteLicenseCommand, DeleteLicenseResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteLicenseHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteLicenseResponse> Handle(
        DeleteLicenseCommand command, CancellationToken cancellationToken)
    {
        await LicenseRules.LockAsync(_context, command.LicenseId, cancellationToken);

        var license = await _context.SoftwareLicenses
            .FirstOrDefaultAsync(l => l.LicenseId == command.LicenseId, cancellationToken)
            ?? throw new KeyNotFoundException("La licencia solicitada no existe.");

        var inUse = await LicenseRules.CountInUseAsync(_context, license.LicenseId, cancellationToken);
        if (inUse > 0)
        {
            throw new BusinessRuleViolationException(
                $"La licencia está asignada a {inUse} equipos. Libérelas antes de eliminarla.");
        }

        await _softDeleteAuditor.DeleteAsync(
            license,
            recordId: license.LicenseId,
            expectedVersion: command.ExpectedVersion,
            tableName: "software_licenses",
            snapshotBeforeDelete: LicenseRules.Snapshot(license),
            cancellationToken);

        return new DeleteLicenseResponse(license.LicenseId, true);
    }
}
