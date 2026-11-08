using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.UpdateTicketStatus;

public sealed class UpdateTicketStatusHandler : ICommandHandler<UpdateTicketStatusCommand, UpdateTicketStatusResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IRolePermissionService _rolePermissions;

    public UpdateTicketStatusHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IRolePermissionService rolePermissions)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _rolePermissions = rolePermissions;
    }

    public async ValueTask<UpdateTicketStatusResponse> Handle(
        UpdateTicketStatusCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var isManager = await TicketAccess.IsManagerAsync(_rolePermissions, _currentUser, cancellationToken);
        var ticket = await TicketAccess.GetVisibleAsync(_context, command.TicketId, userId, isManager, cancellationToken);

        var previousStatus = ticket.Status;
        var newStatus = command.NewStatus.Trim().ToUpperInvariant();

        if (!TicketStatusTransitions.IsAllowed(previousStatus, newStatus, isManager))
            throw new BusinessRuleViolationException(
                $"No se puede pasar el ticket de {previousStatus} a {newStatus}.");

        _context.Entry(ticket).Property(t => t.Version).OriginalValue = command.ExpectedVersion;
        ticket.Status = newStatus;
        ticket.UpdatedBy = userId;

        if (TicketStatusCodes.Terminal.Contains(newStatus))
            ticket.ClosedAt = DateTime.UtcNow;

        // Reabrir: deja de contar el plazo de cierre automático.
        if (previousStatus == TicketStatusCodes.Resuelto && newStatus == TicketStatusCodes.EnAtencion)
            ticket.ResolvedAt = null;

        var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // El motivo queda visible en el hilo (p. ej. "no se resolvió, sigue fallando").
        TicketComment? comment = null;
        if (reason is not null)
        {
            comment = new TicketComment
            {
                TicketId = ticket.TicketId,
                AuthorId = userId,
                Body = reason,
                IsInternal = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.TicketComments.Add(comment);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(ticket).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "tickets",
            recordId: ticket.TicketId,
            action: "UPDATE",
            oldData: new { Status = previousStatus },
            newData: new { ticket.Status, Reason = reason, ticket.ClosedAt },
            cancellationToken);

        if (comment is not null)
        {
            await _auditLog.LogAsync(
                tableName: "ticket_comments",
                recordId: comment.TicketCommentId,
                action: "INSERT",
                oldData: null,
                newData: new { comment.TicketCommentId, comment.TicketId, comment.AuthorId, comment.IsInternal },
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new UpdateTicketStatusResponse(ticket.TicketId, previousStatus, ticket.Status, ticket.Version);
    }
}
