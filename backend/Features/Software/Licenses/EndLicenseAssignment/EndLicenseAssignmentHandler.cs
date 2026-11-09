using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Licenses.Shared;

namespace TecnmInventario.Api.Features.Software.Licenses.EndLicenseAssignment;

public sealed class EndLicenseAssignmentHandler
    : ICommandHandler<EndLicenseAssignmentCommand, EndLicenseAssignmentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public EndLicenseAssignmentHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<EndLicenseAssignmentResponse> Handle(
        EndLicenseAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _context.SoftwareLicenseAssignments
            .FirstOrDefaultAsync(a => a.LicenseAssignmentId == command.LicenseAssignmentId, cancellationToken)
            ?? throw new KeyNotFoundException("La asignación solicitada no existe.");

        if (assignment.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException("La asignación fue modificada por otro usuario mientras tanto.");
        }

        if (assignment.EndedAt is not null)
        {
            throw new BusinessRuleViolationException("La asignación ya fue liberada.");
        }

        var before = LicenseRules.Snapshot(assignment);

        _context.Entry(assignment).Property(a => a.Version).OriginalValue = command.ExpectedVersion;
        assignment.EndedAt = DateTime.UtcNow;
        assignment.UpdatedBy = _currentUser.UserId;

        // UpdatedAt y Version los actualiza el trigger touch_version.
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(assignment).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "software_license_assignments",
            recordId: assignment.LicenseAssignmentId,
            action: "UPDATE",
            oldData: before,
            newData: LicenseRules.Snapshot(assignment),
            cancellationToken);

        return new EndLicenseAssignmentResponse(
            assignment.LicenseAssignmentId, assignment.EndedAt!.Value, assignment.Version);
    }
}
