using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Tickets.AssignTicket;

public sealed class AssignTicketHandler : ICommandHandler<AssignTicketCommand, AssignTicketResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IRolePermissionService _rolePermissions;

    public AssignTicketHandler(
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

    public async ValueTask<AssignTicketResponse> Handle(
        AssignTicketCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        // El endpoint ya exige tickets.manage: el técnico ve cualquier ticket.
        var ticket = await TicketAccess.GetVisibleAsync(_context, command.TicketId, userId, isManager: true, cancellationToken);

        if (!TicketStatusCodes.Open.Contains(ticket.Status))
            throw new BusinessRuleViolationException(
                "Solo se pueden asignar tickets abiertos, en atención o esperando al usuario.");

        if (ticket.AssignedTo == command.AssignedToUserId)
            throw new BusinessRuleViolationException("El ticket ya está asignado a ese técnico.");

        // Mensaje único para "no existe" e "inactivo": no revela qué usuarios existen.
        var technician = await _context.AppUsers
            .Where(u => u.UserId == command.AssignedToUserId && !u.IsDeleted && u.Status == "activo")
            .Select(u => new { u.UserId, u.Username, RoleCode = u.Role.Code })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleViolationException(
                "El técnico especificado no existe o no está activo.");

        if (!await _rolePermissions.HasPermissionAsync(technician.RoleCode, TicketAccess.ManagePermission, cancellationToken))
            throw new BusinessRuleViolationException(
                "El usuario especificado no tiene permisos para atender tickets.");

        var previous = new { ticket.AssignedTo, ticket.Status };

        _context.Entry(ticket).Property(t => t.Version).OriginalValue = command.ExpectedVersion;
        ticket.AssignedTo = technician.UserId;
        ticket.UpdatedBy = userId;
        if (ticket.Status == TicketStatusCodes.Abierto)
            ticket.Status = TicketStatusCodes.EnAtencion;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(ticket).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "tickets",
            recordId: ticket.TicketId,
            action: "UPDATE",
            oldData: previous,
            newData: new { ticket.AssignedTo, AssignedToUsername = technician.Username, ticket.Status },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new AssignTicketResponse(
            ticket.TicketId, technician.UserId, technician.Username, ticket.Status, ticket.Version);
    }
}
