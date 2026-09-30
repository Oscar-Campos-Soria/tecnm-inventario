namespace TecnmInventario.Api.Features.Auth.Login;

public sealed record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    long UserId,
    string Username,
    string RoleCode);
