namespace TecnmInventario.Api.Features.Users.ListUsers;

public sealed record ListUsersResponse(
    long UserId,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string RoleCode,
    string RoleName,
    string Status,
    DateTime CreatedAt,
    int Version);
