using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Infrastructure;

namespace TecnmInventario.Api.Features.Locations.UpdateLocation;

public sealed class UpdateLocationHandler : ICommandHandler<UpdateLocationCommand, UpdateLocationResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateLocationHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<UpdateLocationResponse> Handle(
        UpdateLocationCommand command, CancellationToken cancellationToken)
    {
        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.LocationId == command.LocationId, cancellationToken);

        if (location is null)
        {
            throw new KeyNotFoundException("La ubicación solicitada no existe.");
        }

        _context.Entry(location).Property(l => l.Version).OriginalValue = command.ExpectedVersion;

        location.Name = command.Name.Trim();
        location.LocationTypeId = command.LocationTypeId;
        location.ParentLocationId = command.ParentLocationId;
        location.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(location).ReloadAsync(cancellationToken);

        return new UpdateLocationResponse(location.LocationId, location.Version);
    }
}
