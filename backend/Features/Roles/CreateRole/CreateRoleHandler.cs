using Mediator;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Roles.CreateRole;

public sealed class CreateRoleHandler : ICommandHandler<CreateRoleCommand, CreateRoleResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateRoleHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<CreateRoleResponse> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var role = new Role
        {
            Code = command.Code,
            Name = command.Name,
            Description = command.Description,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateRoleResponse(role.RoleId, role.Code, role.Name, role.Description);
    }
}
