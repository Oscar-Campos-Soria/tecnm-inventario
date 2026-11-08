using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.UpdateTicketPriority;

public sealed class UpdateTicketPriorityHandler : ICommandHandler<UpdateTicketPriorityCommand, UpdateTicketPriorityResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UpdateTicketPriorityHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UpdateTicketPriorityResponse> Handle(
        UpdateTicketPriorityCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var ticket = await TicketAccess.GetVisibleAsync(_context, command.TicketId, userId, isManager: true, cancellationToken);

        if (TicketStatusCodes.Terminal.Contains(ticket.Status))
            throw new BusinessRuleViolationException("No se puede cambiar la prioridad de un ticket cerrado o cancelado.");

        var priorities = await _context.IncidentPriorities
            .Where(p => !p.IsDeleted
                && (p.IncidentPriorityId == command.PriorityId || p.IncidentPriorityId == ticket.IncidentPriorityId))
            .Select(p => new { p.IncidentPriorityId, p.Code })
            .ToListAsync(cancellationToken);

        var newPriority = priorities.FirstOrDefault(p => p.IncidentPriorityId == command.PriorityId)
            ?? throw new BusinessRuleViolationException("La prioridad especificada no existe.");
        var previousCode = priorities.FirstOrDefault(p => p.IncidentPriorityId == ticket.IncidentPriorityId)?.Code;

        if (newPriority.IncidentPriorityId == ticket.IncidentPriorityId)
            throw new BusinessRuleViolationException("El ticket ya tiene esa prioridad.");

        _context.Entry(ticket).Property(t => t.Version).OriginalValue = command.ExpectedVersion;
        ticket.IncidentPriorityId = newPriority.IncidentPriorityId;
        ticket.UpdatedBy = userId;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(ticket).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "tickets",
            recordId: ticket.TicketId,
            action: "UPDATE",
            oldData: new { Priority = previousCode },
            newData: new { Priority = newPriority.Code },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new UpdateTicketPriorityResponse(ticket.TicketId, newPriority.Code, ticket.Version);
    }
}
