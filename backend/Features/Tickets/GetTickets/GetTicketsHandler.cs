using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.GetTickets;

public sealed class GetTicketsHandler
    : IQueryHandler<GetTicketsQuery, PagedResult<GetTicketsResponse>>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRolePermissionService _rolePermissions;

    public GetTicketsHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IRolePermissionService rolePermissions)
    {
        _context = context;
        _currentUser = currentUser;
        _rolePermissions = rolePermissions;
    }

    public async ValueTask<PagedResult<GetTicketsResponse>> Handle(
        GetTicketsQuery query, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var isManager = await TicketAccess.IsManagerAsync(_rolePermissions, _currentUser, cancellationToken);

        var tickets = _context.Tickets.AsNoTracking();

        // Sin tickets.manage: solo los propios, lo pida o no el cliente.
        if (!isManager)
            tickets = tickets.Where(t => t.RequesterId == userId);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToUpperInvariant();
            tickets = tickets.Where(t => t.Status == status);
        }

        if (query.OnlyOpen == true)
        {
            var openCodes = TicketStatusCodes.Open.ToArray();
            tickets = tickets.Where(t => openCodes.Contains(t.Status));
        }

        if (query.CategoryId.HasValue)
        {
            var categoryId = query.CategoryId.Value;
            tickets = tickets.Where(t => t.TicketCategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(query.PriorityCode))
        {
            var priorityCode = query.PriorityCode.Trim().ToUpperInvariant();
            tickets = tickets.Where(t => t.IncidentPriority.Code == priorityCode);
        }

        if (query.AssignedTo.HasValue)
        {
            var assignedTo = query.AssignedTo.Value;
            tickets = tickets.Where(t => t.AssignedTo == assignedTo);
        }

        if (query.Unassigned == true)
            tickets = tickets.Where(t => t.AssignedTo == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            tickets = tickets.Where(t => t.Title.ToLower().Contains(term));
        }

        var totalCount = await tickets.CountAsync(cancellationToken);

        // Bandeja de pendientes: lo urgente arriba y, a igual prioridad, lo más viejo primero.
        // Historial (sin onlyOpen): lo más reciente primero.
        var ordered = query.OnlyOpen == true
            ? tickets
                .OrderBy(t => t.IncidentPriority.Code == IncidentPriorityCodes.Critica ? 0
                    : t.IncidentPriority.Code == IncidentPriorityCodes.Alta ? 1
                    : t.IncidentPriority.Code == IncidentPriorityCodes.Media ? 2
                    : 3)
                .ThenBy(t => t.CreatedAt)
                .ThenBy(t => t.TicketId)
            : tickets
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.TicketId);

        var items = await ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new GetTicketsResponse(
                t.TicketId,
                t.Title,
                t.TicketCategory.Code,
                t.TicketCategory.Name,
                t.IncidentPriority.Code,
                t.IncidentPriority.Name,
                t.Status,
                t.Requester.Username,
                t.Requester.FirstName + " " + t.Requester.LastName,
                t.AssignedToNavigation != null ? t.AssignedToNavigation.Username : null,
                t.Asset != null ? t.Asset.InventoryCode : null,
                t.IncidentId,
                t.CreatedAt,
                t.UpdatedAt,
                t.Version))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetTicketsResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
