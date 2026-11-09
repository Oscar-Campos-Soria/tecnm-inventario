namespace TecnmInventario.Api.Features.Users.UpdateUser;

public sealed record UpdateUserResponse(long UserId, string RoleCode, int Version);
