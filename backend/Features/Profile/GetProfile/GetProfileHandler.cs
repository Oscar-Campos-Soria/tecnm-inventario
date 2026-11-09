using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Profile.GetProfile;

public sealed class GetProfileHandler : IQueryHandler<GetProfileQuery, GetProfileResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetProfileHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<GetProfileResponse> Handle(GetProfileQuery query, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var profile = await _context.AppUsers.AsNoTracking()
            .Where(u => u.UserId == userId && !u.IsDeleted)
            .Select(u => new { u.UserId, u.Username, u.Email, u.FirstName, u.LastName, RoleName = u.Role.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no existe.");

        var links = await _context.UserExternalLogins.AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Provider)
            .Select(l => new ExternalLoginItem(l.Provider, l.Email, l.LinkedAt))
            .ToListAsync(cancellationToken);

        return new GetProfileResponse(
            profile.UserId, profile.Username, profile.Email, profile.FirstName, profile.LastName, profile.RoleName, links);
    }
}
