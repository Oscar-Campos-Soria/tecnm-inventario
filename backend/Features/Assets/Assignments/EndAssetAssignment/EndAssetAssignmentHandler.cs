using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;

public sealed class EndAssetAssignmentHandler
    : ICommandHandler<EndAssetAssignmentCommand, EndAssetAssignmentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EndAssetAssignmentHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<EndAssetAssignmentResponse> Handle(
        EndAssetAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _context.AssetAssignments
            .FirstOrDefaultAsync(
                a => a.AssetAssignmentId == command.AssetAssignmentId &&
                     a.AssetId == command.AssetId,
                cancellationToken);

        if (assignment is null)
        {
            throw new KeyNotFoundException("La asignación solicitada no existe.");
        }

        if (assignment.ReturnedAt is not null)
        {
            throw new BusinessRuleViolationException("La asignación ya fue devuelta.");
        }

        assignment.ReturnedAt = DateTime.UtcNow;
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        return new EndAssetAssignmentResponse(
            assignment.AssetAssignmentId,
            assignment.ReturnedAt.Value);
    }
}
