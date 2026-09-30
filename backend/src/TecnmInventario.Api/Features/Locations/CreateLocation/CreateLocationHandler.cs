using Mediator;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Infrastructure;
using TecnmInventario.Infrastructure.Entities;

namespace TecnmInventario.Api.Features.Locations.CreateLocation;

public sealed class CreateLocationHandler : ICommandHandler<CreateLocationCommand, CreateLocationResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateLocationHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<CreateLocationResponse> Handle(
        CreateLocationCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var location = new Location
        {
            Code = command.Code.Trim(),
            Name = command.Name.Trim(),
            LocationTypeId = command.LocationTypeId,
            ParentLocationId = command.ParentLocationId,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.Locations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateLocationResponse(location.LocationId, location.Code, location.Version);
    }
}
