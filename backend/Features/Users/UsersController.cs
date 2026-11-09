using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Users.ChangeUserStatus;
using TecnmInventario.Api.Features.Users.GetUsers;
using TecnmInventario.Api.Features.Users.ListUsers;
using TecnmInventario.Api.Features.Users.ResetUserPassword;
using TecnmInventario.Api.Features.Users.UpdateUser;

namespace TecnmInventario.Api.Features.Users;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "users.view")]
    public async Task<ActionResult<IReadOnlyList<GetUsersResponse>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? roles,
        CancellationToken cancellationToken)
    {
        // roles=ADMIN,TECNICO (selector de técnicos; antes se filtraba en el cliente).
        var roleCodes = roles?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await _mediator.Send(new GetUsersQuery(search, roleCodes), cancellationToken);
        return Ok(result);
    }

    // ---------- Administración (solo ADMIN: users.manage). Alta: POST /api/auth/register. ----------

    [HttpGet("manage")]
    [Authorize(Policy = "users.manage")]
    public async Task<ActionResult<PagedResult<ListUsersResponse>>> ListUsers(
        [FromQuery] PagedRequest pagination,
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new ListUsersQuery(pagination.PageNumber, pagination.PageSize, search, role, status), cancellationToken));

    public sealed record UpdateUserBody(string Email, string FirstName, string LastName, string RoleCode);

    [HttpPut("{userId:long}")]
    [Authorize(Policy = "users.manage")]
    public async Task<ActionResult<UpdateUserResponse>> UpdateUser(
        long userId, [FromQuery] int expectedVersion, UpdateUserBody body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new UpdateUserCommand(userId, expectedVersion, body.Email, body.FirstName, body.LastName, body.RoleCode), cancellationToken));

    public sealed record ChangeUserStatusBody(string Status);

    [HttpPatch("{userId:long}/status")]
    [Authorize(Policy = "users.manage")]
    public async Task<ActionResult<ChangeUserStatusResponse>> ChangeStatus(
        long userId, [FromQuery] int expectedVersion, ChangeUserStatusBody body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new ChangeUserStatusCommand(userId, expectedVersion, body.Status), cancellationToken));

    public sealed record ResetUserPasswordBody(string NewPassword);

    [HttpPut("{userId:long}/password")]
    [Authorize(Policy = "users.manage")]
    public async Task<ActionResult<ResetUserPasswordResponse>> ResetPassword(
        long userId, [FromQuery] int expectedVersion, ResetUserPasswordBody body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new ResetUserPasswordCommand(userId, expectedVersion, body.NewPassword), cancellationToken));
}
