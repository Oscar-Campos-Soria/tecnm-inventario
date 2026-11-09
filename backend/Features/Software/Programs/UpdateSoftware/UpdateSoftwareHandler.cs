using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Programs.Shared;

namespace TecnmInventario.Api.Features.Software.Programs.UpdateSoftware;

public sealed class UpdateSoftwareHandler : ICommandHandler<UpdateSoftwareCommand, UpdateSoftwareResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UpdateSoftwareHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UpdateSoftwareResponse> Handle(
        UpdateSoftwareCommand command, CancellationToken cancellationToken)
    {
        var software = await _context.Softwares
            .FirstOrDefaultAsync(s => s.SoftwareId == command.SoftwareId, cancellationToken)
            ?? throw new KeyNotFoundException("El programa solicitado no existe.");

        // Versión vieja siempre es 409, aunque los datos sean idénticos.
        if (software.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException("El programa fue modificado por otro usuario mientras tanto.");
        }

        var name = command.Name.Trim();
        var vendor = SoftwareNames.NullIfBlank(command.Vendor);

        // Un PUT idéntico no genera UPDATE ni auditoría.
        if ((software.Name, software.Vendor, software.SoftwareTypeId) == (name, vendor, command.SoftwareTypeId))
        {
            return new UpdateSoftwareResponse(software.SoftwareId, software.Name, software.Vendor, software.Version);
        }

        var before = new { software.Name, software.Vendor, software.SoftwareTypeId, software.Version };

        _context.Entry(software).Property(s => s.Version).OriginalValue = command.ExpectedVersion;
        software.Name = name;
        software.Vendor = vendor;
        software.SoftwareTypeId = command.SoftwareTypeId;
        software.UpdatedBy = _currentUser.UserId;

        // UpdatedAt y Version los actualiza el trigger touch_version.
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(software).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "software",
            recordId: software.SoftwareId,
            action: "UPDATE",
            oldData: before,
            newData: new { software.Name, software.Vendor, software.SoftwareTypeId, software.Version },
            cancellationToken);

        return new UpdateSoftwareResponse(software.SoftwareId, software.Name, software.Vendor, software.Version);
    }
}
