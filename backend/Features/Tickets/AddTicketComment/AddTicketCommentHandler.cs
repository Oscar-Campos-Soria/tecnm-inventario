using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.AddTicketComment;

public sealed class AddTicketCommentHandler : ICommandHandler<AddTicketCommentCommand, AddTicketCommentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IRolePermissionService _rolePermissions;

    public AddTicketCommentHandler(
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

    public async ValueTask<AddTicketCommentResponse> Handle(
        AddTicketCommentCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var isManager = await TicketAccess.IsManagerAsync(_rolePermissions, _currentUser, cancellationToken);
        var ticket = await TicketAccess.GetVisibleAsync(_context, command.TicketId, userId, isManager, cancellationToken);

        if (command.IsInternal && !isManager)
            throw new ForbiddenAccessException("Solo el personal de soporte puede dejar notas internas.");

        if (TicketStatusCodes.Terminal.Contains(ticket.Status))
            throw new BusinessRuleViolationException("No se puede comentar un ticket cerrado o cancelado.");

        // El solicitante respondió lo que se le pidió: vuelve a la cola del técnico.
        var previousStatus = ticket.Status;
        var reactivate = ticket.RequesterId == userId
            && !command.IsInternal
            && ticket.Status == TicketStatusCodes.EsperandoUsuario;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var comment = new TicketComment
        {
            TicketId = ticket.TicketId,
            AuthorId = userId,
            Body = command.Body.Trim(),
            IsInternal = command.IsInternal,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketComments.Add(comment);

        if (reactivate)
        {
            ticket.Status = TicketStatusCodes.EnAtencion;
            ticket.UpdatedBy = userId;
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (reactivate)
            await _context.Entry(ticket).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "ticket_comments",
            recordId: comment.TicketCommentId,
            action: "INSERT",
            oldData: null,
            newData: new { comment.TicketCommentId, comment.TicketId, comment.AuthorId, comment.IsInternal },
            cancellationToken);

        if (reactivate)
        {
            await _auditLog.LogAsync(
                tableName: "tickets",
                recordId: ticket.TicketId,
                action: "UPDATE",
                oldData: new { Status = previousStatus },
                newData: new { ticket.Status, Reason = "Respuesta del solicitante" },
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new AddTicketCommentResponse(
            comment.TicketCommentId, ticket.TicketId, comment.IsInternal, comment.CreatedAt,
            ticket.Status, ticket.Version);
    }
}
