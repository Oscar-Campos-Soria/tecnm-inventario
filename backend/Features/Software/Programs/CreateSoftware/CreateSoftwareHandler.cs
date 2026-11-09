using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Programs.Shared;
using SoftwareEntity = TecnmInventario.Api.Core.Entities.Software;

namespace TecnmInventario.Api.Features.Software.Programs.CreateSoftware;

public sealed class CreateSoftwareHandler : ICommandHandler<CreateSoftwareCommand, CreateSoftwareResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CreateSoftwareHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CreateSoftwareResponse> Handle(
        CreateSoftwareCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var software = new SoftwareEntity
        {
            Name = command.Name.Trim(),
            Vendor = SoftwareNames.NullIfBlank(command.Vendor),
            SoftwareTypeId = command.SoftwareTypeId,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        _context.Softwares.Add(software);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "software",
            recordId: software.SoftwareId,
            action: "INSERT",
            oldData: null,
            newData: new { software.Name, software.Vendor, software.SoftwareTypeId, software.Version },
            cancellationToken);

        return new CreateSoftwareResponse(software.SoftwareId, software.Name, software.Vendor, software.Version);
    }
}
