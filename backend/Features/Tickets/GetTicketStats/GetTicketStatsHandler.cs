using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.GetTicketStats;

public sealed class GetTicketStatsHandler : IQueryHandler<GetTicketStatsQuery, GetTicketStatsResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRolePermissionService _rolePermissions;

    public GetTicketStatsHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IRolePermissionService rolePermissions)
    {
        _context = context;
        _currentUser = currentUser;
        _rolePermissions = rolePermissions;
    }

    public async ValueTask<GetTicketStatsResponse> Handle(GetTicketStatsQuery query, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var tickets = _context.Tickets.AsNoTracking();
        if (!await TicketAccess.IsManagerAsync(_rolePermissions, _currentUser, cancellationToken))
            tickets = tickets.Where(t => t.RequesterId == userId);

        var (from, to) = (query.From, query.To);
        var open = TicketStatusCodes.Open.ToArray();
        var created = tickets.Where(t => t.CreatedAt >= from && t.CreatedAt < to);
        var resolved = tickets.Where(t => t.ResolvedAt >= from && t.ResolvedAt < to);

        // Restas de fecha en memoria: igual en PostgreSQL y en las pruebas.
        var hours = (await resolved.Select(t => new { t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value }).ToListAsync(cancellationToken))
            .Select(t => (t.ResolvedAt - t.CreatedAt).TotalHours).ToList();
        return new GetTicketStatsResponse(
            from,
            to,
            await created.CountAsync(cancellationToken),
            hours.Count,
            await tickets.CountAsync(t => t.ClosedAt >= from && t.ClosedAt < to, cancellationToken),
            await tickets.CountAsync(t => t.Status == TicketStatusCodes.Cancelado && t.UpdatedAt >= from && t.UpdatedAt < to, cancellationToken),
            await created.CountAsync(t => open.Contains(t.Status), cancellationToken),
            hours.Count == 0 ? null : Math.Round(hours.Average(), 1),
            await tickets.CountAsync(t => open.Contains(t.Status), cancellationToken),
            await created.GroupBy(t => t.IncidentPriority.Code).Select(g => new CodeCount(g.Key, g.Count())).ToListAsync(cancellationToken),
            await created.GroupBy(t => t.TicketCategory.Code).Select(g => new CodeCount(g.Key, g.Count())).ToListAsync(cancellationToken));
    }
}
