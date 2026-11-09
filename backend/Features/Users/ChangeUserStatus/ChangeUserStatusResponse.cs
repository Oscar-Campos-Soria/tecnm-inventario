namespace TecnmInventario.Api.Features.Users.ChangeUserStatus;

public sealed record ChangeUserStatusResponse(long UserId, string Status, int SessionsRevoked, int Version);
