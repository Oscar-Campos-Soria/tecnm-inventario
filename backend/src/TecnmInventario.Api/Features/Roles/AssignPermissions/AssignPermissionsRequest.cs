namespace TecnmInventario.Api.Features.Roles.AssignPermissions;

public sealed record AssignPermissionsRequest(IReadOnlyList<string> PermissionCodes);
