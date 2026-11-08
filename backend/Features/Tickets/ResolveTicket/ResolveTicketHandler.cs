using Mediator;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.ResolveTicket;

public sealed class ResolveTicketHandler : ICommandHandler<ResolveTicketCommand, ResolveTicketResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly TicketOptions _options;

    public ResolveTicketHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IOptions<TicketOptions> options)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _options = options.Value;
    }

    public async ValueTask<ResolveTicketResponse> Handle(
        ResolveTicketCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var ticket = await TicketAccess.GetVisibleAsync(_context, command.TicketId, userId, isManager: true, cancellationToken);

        if (!TicketStatusCodes.Open.Contains(ticket.Status))
            throw new BusinessRuleViolationException("Solo se puede resolver un ticket pendiente de atención.");

        var previous = new { ticket.Status, ticket.AssignedTo };
        var now = DateTime.UtcNow;

        _context.Entry(ticket).Property(t => t.Version).OriginalValue = command.ExpectedVersion;
        ticket.Status = TicketStatusCodes.Resuelto;
        ticket.Resolution = command.Resolution.Trim();
        ticket.ResolvedAt = now;
        ticket.UpdatedBy = userId;
        // Resuelto sin asignar (solución rápida): queda a nombre de quien lo resolvió.
        ticket.AssignedTo ??= userId;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(ticket).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "tickets",
            recordId: ticket.TicketId,
            action: "UPDATE",
            oldData: previous,
            newData: new { ticket.Status, ticket.AssignedTo, ticket.Resolution, ticket.ResolvedAt },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ResolveTicketResponse(
            ticket.TicketId, ticket.Status, now, now.AddDays(_options.AutoCloseDays), ticket.Version);
    }
}
