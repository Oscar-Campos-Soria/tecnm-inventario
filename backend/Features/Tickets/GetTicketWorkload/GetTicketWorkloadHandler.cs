using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.GetTicketWorkload;

public sealed class GetTicketWorkloadHandler : IQueryHandler<GetTicketWorkloadQuery, GetTicketWorkloadResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRolePermissionService _rolePermissions;

    public GetTicketWorkloadHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IRolePermissionService rolePermissions)
    {
        _context = context;
        _currentUser = currentUser;
        _rolePermissions = rolePermissions;
    }

    public async ValueTask<GetTicketWorkloadResponse> Handle(GetTicketWorkloadQuery query, CancellationToken cancellationToken)
    {
        // La carga de otros es información de gestión: solo quien administra tickets.
        if (!await TicketAccess.IsManagerAsync(_rolePermissions, _currentUser, cancellationToken))
            throw new ForbiddenAccessException("No tienes permiso para ver la carga de trabajo.");

        var openTickets = TicketStatusCodes.Open.ToArray();
        var openIncidents = IncidentStatusCodes.Open.ToArray();

        var tickets = await _context.Tickets.AsNoTracking()
            .Where(t => openTickets.Contains(t.Status))
            .GroupBy(t => t.AssignedTo)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                Critical = g.Count(t => t.IncidentPriority.Code == IncidentPriorityCodes.Critica),
                Oldest = g.Min(t => t.CreatedAt),
            })
            .ToListAsync(cancellationToken);

        var incidents = await _context.Incidents.AsNoTracking()
            .Where(i => !i.IsDeleted && openIncidents.Contains(i.IncidentStatus.Code))
            .GroupBy(i => i.AssignedTo)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var ids = tickets.Select(t => t.UserId).Concat(incidents.Select(i => i.UserId)).OfType<long>().Distinct().ToList();
        var users = await _context.AppUsers.AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .Select(u => new { u.UserId, u.Username, FullName = u.FirstName + " " + u.LastName })
            .ToListAsync(cancellationToken);

        var assignees = users.Select(u =>
            {
                var t = tickets.FirstOrDefault(x => x.UserId == u.UserId);
                return new AssigneeWorkload(u.Username, u.FullName, t?.Count ?? 0, t?.Critical ?? 0,
                    incidents.FirstOrDefault(x => x.UserId == u.UserId)?.Count ?? 0, t?.Oldest);
            })
            .OrderByDescending(a => a.OpenTickets + a.OpenIncidents)
            .ThenBy(a => a.Username)
            .ToList();

        return new GetTicketWorkloadResponse(assignees,
            tickets.FirstOrDefault(t => t.UserId is null)?.Count ?? 0,
            incidents.FirstOrDefault(i => i.UserId is null)?.Count ?? 0);
    }
}
