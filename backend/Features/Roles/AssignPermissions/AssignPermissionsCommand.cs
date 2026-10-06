using Mediator;

namespace TecnmInventario.Api.Features.Roles.AssignPermissions;

public sealed record AssignPermissionsCommand(
    long RoleId,
    IReadOnlyList<string> PermissionCodes) : ICommand<AssignPermissionsResponse>;
