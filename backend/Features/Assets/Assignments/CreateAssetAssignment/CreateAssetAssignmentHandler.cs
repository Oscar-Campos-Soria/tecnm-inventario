using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.Assignments.CreateAssetAssignment;

public sealed class CreateAssetAssignmentHandler
    : ICommandHandler<CreateAssetAssignmentCommand, CreateAssetAssignmentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateAssetAssignmentHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<CreateAssetAssignmentResponse> Handle(
        CreateAssetAssignmentCommand command, CancellationToken cancellationToken)
    {
        var asset = await _context.Assets
            .FirstOrDefaultAsync(
                a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        var hasActiveAssignment = await _context.AssetAssignments
            .AnyAsync(
                a => a.AssetId == command.AssetId && a.ReturnedAt == null,
                cancellationToken);

        if (hasActiveAssignment)
        {
            throw new BusinessRuleViolationException(
                "El activo ya tiene una asignación activa. Debe devolverse antes de asignarlo.");
        }

        var now = DateTime.UtcNow;

        var assignment = new AssetAssignment
        {
            AssetId = asset.AssetId,
            AssignedTo = command.AssignedTo,
            AssignmentTypeId = command.AssignmentTypeId,
            LocationId = command.LocationId,
            AssignedAt = command.AssignedAt ?? now,
            Notes = command.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        _context.AssetAssignments.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateAssetAssignmentResponse(
            assignment.AssetAssignmentId,
            assignment.AssetId,
            assignment.AssignedTo,
            assignment.AssignedAt);
    }
}
