namespace TecnmInventario.Api.Features.Profile.GetProfile;

public sealed record ExternalLoginItem(string Provider, string Email, DateTime LinkedAt);

public sealed record GetProfileResponse(
    long UserId,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string RoleName,
    IReadOnlyList<ExternalLoginItem> ExternalLogins);
