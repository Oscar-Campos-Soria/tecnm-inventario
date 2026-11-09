using Mediator;

namespace TecnmInventario.Api.Features.Roles.GetPermissions;

public sealed record GetPermissionsQuery() : IQuery<IReadOnlyList<PermissionItem>>;
