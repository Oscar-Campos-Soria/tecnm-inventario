using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Licenses.Shared;

namespace TecnmInventario.Api.Features.Software.Licenses.UpdateLicense;

public sealed class UpdateLicenseHandler : ICommandHandler<UpdateLicenseCommand, UpdateLicenseResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UpdateLicenseHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UpdateLicenseResponse> Handle(
        UpdateLicenseCommand command, CancellationToken cancellationToken)
    {
        await LicenseRules.LockAsync(_context, command.LicenseId, cancellationToken);

        var license = await _context.SoftwareLicenses
            .FirstOrDefaultAsync(l => l.LicenseId == command.LicenseId, cancellationToken)
            ?? throw new KeyNotFoundException("La licencia solicitada no existe.");

        if (license.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException("La licencia fue modificada por otro usuario mientras tanto.");
        }

        var key = command.LicenseKey is null ? license.LicenseKey : LicenseRules.NullIfBlank(command.LicenseKey);
        var notes = LicenseRules.NullIfBlank(command.Notes);

        var current = (license.LicenseTypeId, license.LicenseKey, license.QuantityPurchased,
            license.ValidFrom, license.ValidUntil, license.SupplierId, license.Notes);
        var requested = (command.LicenseTypeId, key, command.QuantityPurchased,
            command.ValidFrom, command.ValidUntil, command.SupplierId, notes);
        if (current == requested)
        {
            return new UpdateLicenseResponse(license.LicenseId, license.Version);
        }

        if (command.QuantityPurchased is int quantity)
        {
            var inUse = await LicenseRules.CountInUseAsync(_context, license.LicenseId, cancellationToken);
            if (quantity < inUse)
            {
                throw new BusinessRuleViolationException(
                    $"La licencia está asignada a {inUse} equipos; la cantidad no puede ser menor. " +
                    "Libere asignaciones antes de reducirla.");
            }
        }

        var before = LicenseRules.Snapshot(license);

        _context.Entry(license).Property(l => l.Version).OriginalValue = command.ExpectedVersion;
        license.LicenseTypeId = command.LicenseTypeId;
        license.LicenseKey = key;
        license.QuantityPurchased = command.QuantityPurchased;
        license.ValidFrom = command.ValidFrom;
        license.ValidUntil = command.ValidUntil;
        license.SupplierId = command.SupplierId;
        license.Notes = notes;
        license.UpdatedBy = _currentUser.UserId;

        // UpdatedAt y Version los actualiza el trigger touch_version.
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(license).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "software_licenses",
            recordId: license.LicenseId,
            action: "UPDATE",
            oldData: before,
            newData: LicenseRules.Snapshot(license),
            cancellationToken);

        return new UpdateLicenseResponse(license.LicenseId, license.Version);
    }
}
