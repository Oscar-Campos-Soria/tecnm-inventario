using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Software.Programs.DeleteSoftware;

public sealed class DeleteSoftwareHandler : ICommandHandler<DeleteSoftwareCommand, DeleteSoftwareResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteSoftwareHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteSoftwareResponse> Handle(
        DeleteSoftwareCommand command, CancellationToken cancellationToken)
    {
        var software = await _context.Softwares
            .FirstOrDefaultAsync(s => s.SoftwareId == command.SoftwareId, cancellationToken)
            ?? throw new KeyNotFoundException("El programa solicitado no existe.");

        if (await _context.SoftwareLicenses.AnyAsync(l => l.SoftwareId == software.SoftwareId, cancellationToken))
        {
            throw new BusinessRuleViolationException(
                "El programa tiene licencias registradas. Elimínelas antes de borrar el programa.");
        }

        if (await _context.AssetSoftwares.AnyAsync(
                i => i.SoftwareId == software.SoftwareId && i.IsCurrent && !i.IsDeleted, cancellationToken))
        {
            throw new BusinessRuleViolationException(
                "El programa sigue instalado en uno o más equipos. Desinstálelo antes de borrarlo.");
        }

        await _softDeleteAuditor.DeleteAsync(
            software,
            recordId: software.SoftwareId,
            expectedVersion: command.ExpectedVersion,
            tableName: "software",
            snapshotBeforeDelete: new
            {
                software.SoftwareId, software.Name, software.Vendor, software.SoftwareTypeId,
                software.CreatedAt, software.CreatedBy, software.Version
            },
            cancellationToken);

        return new DeleteSoftwareResponse(software.SoftwareId, true);
    }
}
