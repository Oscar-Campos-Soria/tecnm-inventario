using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.LinkTicketIncident;

public sealed class LinkTicketIncidentHandler : ICommandHandler<LinkTicketIncidentCommand, LinkTicketIncidentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public LinkTicketIncidentHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<LinkTicketIncidentResponse> Handle(
        LinkTicketIncidentCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var ticket = await TicketAccess.GetVisibleAsync(_context, command.TicketId, userId, isManager: true, cancellationToken);

        if (TicketStatusCodes.Terminal.Contains(ticket.Status))
            throw new BusinessRuleViolationException("No se puede vincular un incidente a un ticket cerrado o cancelado.");

        if (ticket.IncidentId == command.IncidentId)
            throw new BusinessRuleViolationException("El ticket ya está vinculado a ese incidente.");

        var incident = await _context.Incidents
            .Where(i => i.IncidentId == command.IncidentId)
            .Select(i => new { i.IncidentId, i.AssetId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleViolationException("El incidente especificado no existe.");

        if (ticket.AssetId.HasValue && ticket.AssetId != incident.AssetId)
            throw new BusinessRuleViolationException(
                "El incidente pertenece a un activo distinto al del ticket.");

        var previous = new { ticket.IncidentId, ticket.AssetId };

        _context.Entry(ticket).Property(t => t.Version).OriginalValue = command.ExpectedVersion;
        ticket.IncidentId = incident.IncidentId;
        // Ticket sin activo (p. ej. "mi compu no prende"): toma el del incidente.
        ticket.AssetId ??= incident.AssetId;
        ticket.UpdatedBy = userId;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(ticket).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "tickets",
            recordId: ticket.TicketId,
            action: "UPDATE",
            oldData: previous,
            newData: new { ticket.IncidentId, ticket.AssetId },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new LinkTicketIncidentResponse(ticket.TicketId, incident.IncidentId, incident.AssetId, ticket.Version);
    }
}
