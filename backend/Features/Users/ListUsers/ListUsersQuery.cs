using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Users.ListUsers;

public sealed record ListUsersQuery(
    int PageNumber,
    int PageSize,
    string? Search,
    string? RoleCode,
    string? Status) : IQuery<PagedResult<ListUsersResponse>>;
