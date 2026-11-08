using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.GetInventorySessions;

public sealed class GetInventorySessionsHandler
    : IQueryHandler<GetInventorySessionsQuery, PagedResult<InventorySessionSummary>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetInventorySessionsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<InventorySessionSummary>> Handle(
        GetInventorySessionsQuery query, CancellationToken cancellationToken)
    {
        var sessions = _context.InventorySessions.AsNoTracking();
        var totalCount = await sessions.CountAsync(cancellationToken);

        var items = await sessions
            .OrderByDescending(s => s.StartedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new InventorySessionSummary(
                s.InventorySessionId,
                s.Name,
                s.Location != null ? s.Location.Name : null,
                s.Status,
                s.StartedAt,
                s.EndedAt,
                s.InventoryRecords.Count,
                s.InventoryRecords.Count(r => r.CheckedAt != null),
                s.InventoryRecords.Count(r => r.CheckedAt != null && r.Found),
                s.CreatedByNavigation != null ? s.CreatedByNavigation.Username : null))
            .ToListAsync(cancellationToken);

        return new PagedResult<InventorySessionSummary>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
