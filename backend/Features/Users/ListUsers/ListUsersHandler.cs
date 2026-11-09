using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Users.ListUsers;

/// <summary>Lista para administración: incluye inactivos y suspendidos (el selector GET /api/users no).</summary>
public sealed class ListUsersHandler : IQueryHandler<ListUsersQuery, PagedResult<ListUsersResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public ListUsersHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<ListUsersResponse>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var users = _context.AppUsers.AsNoTracking().Where(u => !u.IsDeleted);

        var search = (query.Search ?? "").Trim();
        if (search != "")
        {
            users = users.Where(u =>
                EF.Functions.ILike(u.Username, $"%{search}%") ||
                EF.Functions.ILike(u.Email, $"%{search}%") ||
                EF.Functions.ILike(u.FirstName + " " + u.LastName, $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.RoleCode))
        {
            var role = query.RoleCode.Trim().ToUpperInvariant();
            users = users.Where(u => u.Role.Code == role);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
            users = users.Where(u => u.Status == query.Status);

        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(u => u.Username)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new ListUsersResponse(
                u.UserId, u.Username, u.Email, u.FirstName, u.LastName,
                u.Role.Code, u.Role.Name, u.Status, u.CreatedAt, u.Version))
            .ToListAsync(cancellationToken);

        return new PagedResult<ListUsersResponse>(items, query.PageNumber, query.PageSize, total);
    }
}
