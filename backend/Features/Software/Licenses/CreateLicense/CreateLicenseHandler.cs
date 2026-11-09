using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Licenses.Shared;

namespace TecnmInventario.Api.Features.Software.Licenses.CreateLicense;

public sealed class CreateLicenseHandler : ICommandHandler<CreateLicenseCommand, CreateLicenseResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CreateLicenseHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CreateLicenseResponse> Handle(
        CreateLicenseCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var license = new SoftwareLicense
        {
            SoftwareId = command.SoftwareId,
            LicenseTypeId = command.LicenseTypeId,
            LicenseKey = LicenseRules.NullIfBlank(command.LicenseKey),
            QuantityPurchased = command.QuantityPurchased,
            ValidFrom = command.ValidFrom,
            ValidUntil = command.ValidUntil,
            SupplierId = command.SupplierId,
            Notes = LicenseRules.NullIfBlank(command.Notes),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        _context.SoftwareLicenses.Add(license);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "software_licenses",
            recordId: license.LicenseId,
            action: "INSERT",
            oldData: null,
            newData: LicenseRules.Snapshot(license),
            cancellationToken);

        return new CreateLicenseResponse(license.LicenseId, license.SoftwareId, license.Version);
    }
}
