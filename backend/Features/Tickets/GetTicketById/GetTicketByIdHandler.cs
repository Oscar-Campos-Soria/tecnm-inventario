using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.GetTicketById;

public sealed class GetTicketByIdHandler : IQueryHandler<GetTicketByIdQuery, GetTicketByIdResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRolePermissionService _rolePermissions;
    private readonly TicketOptions _options;

    public GetTicketByIdHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IRolePermissionService rolePermissions,
        IOptions<TicketOptions> options)
    {
        _context = context;
        _currentUser = currentUser;
        _rolePermissions = rolePermissions;
        _options = options.Value;
    }

    public async ValueTask<GetTicketByIdResponse> Handle(
        GetTicketByIdQuery query, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var isManager = await TicketAccess.IsManagerAsync(_rolePermissions, _currentUser, cancellationToken);

        var ticket = await _context.Tickets.AsNoTracking()
            .Where(t => t.TicketId == query.TicketId && (isManager || t.RequesterId == userId))
            .Select(t => new
            {
                t.TicketId,
                t.Title,
                t.Description,
                t.Resolution,
                t.TicketCategoryId,
                CategoryCode = t.TicketCategory.Code,
                CategoryName = t.TicketCategory.Name,
                t.IncidentPriorityId,
                PriorityCode = t.IncidentPriority.Code,
                PriorityName = t.IncidentPriority.Name,
                t.Status,
                t.RequesterId,
                RequesterUsername = t.Requester.Username,
                RequesterName = t.Requester.FirstName + " " + t.Requester.LastName,
                t.AssignedTo,
                AssignedToUsername = t.AssignedToNavigation != null ? t.AssignedToNavigation.Username : null,
                AssignedToName = t.AssignedToNavigation != null
                    ? t.AssignedToNavigation.FirstName + " " + t.AssignedToNavigation.LastName
                    : null,
                t.AssetId,
                AssetInventoryCode = t.Asset != null ? t.Asset.InventoryCode : null,
                t.LocationId,
                LocationName = t.Location != null ? t.Location.Name : null,
                t.IncidentId,
                IncidentStatusCode = t.Incident != null ? t.Incident.IncidentStatus.Code : null,
                IncidentStatusName = t.Incident != null ? t.Incident.IncidentStatus.Name : null,
                t.CreatedAt,
                t.UpdatedAt,
                t.ResolvedAt,
                t.ClosedAt,
                t.Version
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException(TicketAccess.NotFoundMessage);

        // Las notas internas nunca salen del servidor hacia quien no atiende tickets.
        var comments = await _context.TicketComments.AsNoTracking()
            .Where(c => c.TicketId == ticket.TicketId && (isManager || !c.IsInternal))
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.TicketCommentId)
            .Select(c => new TicketCommentItem(
                c.TicketCommentId,
                c.AuthorId,
                c.Author.Username,
                c.Author.FirstName + " " + c.Author.LastName,
                c.Body,
                c.IsInternal,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        DateTime? autoCloseAt = ticket.Status == TicketStatusCodes.Resuelto && ticket.ResolvedAt.HasValue
            ? ticket.ResolvedAt.Value.AddDays(_options.AutoCloseDays)
            : null;

        return new GetTicketByIdResponse(
            ticket.TicketId, ticket.Title, ticket.Description, ticket.Resolution,
            ticket.TicketCategoryId, ticket.CategoryCode, ticket.CategoryName,
            ticket.IncidentPriorityId, ticket.PriorityCode, ticket.PriorityName,
            ticket.Status,
            ticket.RequesterId, ticket.RequesterUsername, ticket.RequesterName,
            ticket.AssignedTo, ticket.AssignedToUsername, ticket.AssignedToName,
            ticket.AssetId, ticket.AssetInventoryCode,
            ticket.LocationId, ticket.LocationName,
            ticket.IncidentId, ticket.IncidentStatusCode, ticket.IncidentStatusName,
            ticket.CreatedAt, ticket.UpdatedAt, ticket.ResolvedAt, ticket.ClosedAt,
            autoCloseAt, isManager, comments, ticket.Version);
    }
}
