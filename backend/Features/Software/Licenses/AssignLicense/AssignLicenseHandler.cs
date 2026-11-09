using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Licenses.Shared;

namespace TecnmInventario.Api.Features.Software.Licenses.AssignLicense;

public sealed class AssignLicenseHandler : ICommandHandler<AssignLicenseCommand, AssignLicenseResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public AssignLicenseHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<AssignLicenseResponse> Handle(
        AssignLicenseCommand command, CancellationToken cancellationToken)
    {
        // Primero el bloqueo: el conteo de "en uso" de abajo debe ser el definitivo.
        await LicenseRules.LockAsync(_context, command.LicenseId, cancellationToken);

        var license = await _context.SoftwareLicenses
            .FirstOrDefaultAsync(l => l.LicenseId == command.LicenseId, cancellationToken)
            ?? throw new KeyNotFoundException("La licencia solicitada no existe.");

        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El activo solicitado no existe.");

        if (asset.AssetStatus.Code == AssetStatusCodes.DadoDeBaja)
        {
            throw new BusinessRuleViolationException("No se puede asignar una licencia a un activo dado de baja.");
        }

        if (license.ValidUntil < LicenseRules.Today)
        {
            throw new BusinessRuleViolationException(
                $"La licencia venció el {license.ValidUntil:yyyy-MM-dd}; no se puede asignar.");
        }

        if (await _context.SoftwareLicenseAssignments.AnyAsync(
                a => a.LicenseId == license.LicenseId && a.AssetId == asset.AssetId && a.EndedAt == null,
                cancellationToken))
        {
            throw new BusinessRuleViolationException("La licencia ya está asignada a este equipo.");
        }

        var inUse = await LicenseRules.CountInUseAsync(_context, license.LicenseId, cancellationToken);
        if (license.QuantityPurchased is int quantity && inUse >= quantity)
        {
            throw new BusinessRuleViolationException(
                $"No hay licencias disponibles: {inUse} de {quantity} en uso.");
        }

        var now = DateTime.UtcNow;
        var assignment = new SoftwareLicenseAssignment
        {
            LicenseId = license.LicenseId,
            AssetId = asset.AssetId,
            AssignedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        _context.SoftwareLicenseAssignments.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "software_license_assignments",
            recordId: assignment.LicenseAssignmentId,
            action: "INSERT",
            oldData: null,
            newData: LicenseRules.Snapshot(assignment),
            cancellationToken);

        return new AssignLicenseResponse(
            assignment.LicenseAssignmentId, license.LicenseId, asset.AssetId,
            inUse + 1, license.QuantityPurchased, assignment.Version);
    }
}
