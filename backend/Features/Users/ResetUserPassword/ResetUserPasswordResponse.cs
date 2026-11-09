namespace TecnmInventario.Api.Features.Users.ResetUserPassword;

public sealed record ResetUserPasswordResponse(long UserId, int SessionsRevoked, int Version);
