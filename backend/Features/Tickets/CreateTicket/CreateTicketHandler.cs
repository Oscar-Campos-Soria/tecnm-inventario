using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Tickets.CreateTicket;

public sealed class CreateTicketHandler : ICommandHandler<CreateTicketCommand, CreateTicketResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IRolePermissionService _permissions;

    public CreateTicketHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IRolePermissionService permissions)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _permissions = permissions;
    }

    public async ValueTask<CreateTicketResponse> Handle(
        CreateTicketCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var categoryExists = await _context.TicketCategories
            .AnyAsync(c => c.TicketCategoryId == command.CategoryId && !c.IsDeleted, cancellationToken);
        if (!categoryExists)
            throw new BusinessRuleViolationException("El tipo de ticket especificado no existe.");

        // Sin prioridad explícita: MEDIA. El técnico la corrige si hace falta.
        var priority = await _context.IncidentPriorities
            .Where(p => !p.IsDeleted && (command.PriorityId.HasValue
                ? p.IncidentPriorityId == command.PriorityId.Value
                : p.Code == IncidentPriorityCodes.Media))
            .Select(p => new { p.IncidentPriorityId, p.Code })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleViolationException("La prioridad especificada no existe.");

        if (command.AssetId.HasValue)
        {
            var assetStatus = await _context.Assets
                .Where(a => a.AssetId == command.AssetId.Value && !a.IsDeleted)
                .Select(a => a.AssetStatus.Code)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleViolationException("El activo especificado no existe.");

            if (assetStatus == AssetStatusCodes.DadoDeBaja)
                throw new BusinessRuleViolationException(
                    "No se puede levantar un ticket sobre un activo dado de baja.");

            // Quien no ve el inventario (usuario general) solo puede ligar sus propios equipos.
            var canSeeAll = _currentUser.RoleCode is { } role
                && await _permissions.HasPermissionAsync(role, "assets.view", cancellationToken);
            if (!canSeeAll && !await _context.AssetAssignments.AnyAsync(a =>
                    a.AssetId == command.AssetId.Value && a.AssignedTo == userId && a.ReturnedAt == null, cancellationToken))
                throw new BusinessRuleViolationException("Solo puedes reportar equipos que tienes asignados.");
        }

        if (command.LocationId.HasValue)
        {
            var locationExists = await _context.Locations
                .AnyAsync(l => l.LocationId == command.LocationId.Value && !l.IsDeleted, cancellationToken);
            if (!locationExists)
                throw new BusinessRuleViolationException("La ubicación especificada no existe.");
        }

        // Transacción explícita: ticket + audit_log se confirman juntos.
        // El ticket no toca el estado del activo (eso es trabajo de Incidents).
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var ticket = new Ticket
        {
            RequesterId = userId,
            TicketCategoryId = command.CategoryId,
            IncidentPriorityId = priority.IncidentPriorityId,
            Status = TicketStatusCodes.Abierto,
            AssetId = command.AssetId,
            LocationId = command.LocationId,
            Title = command.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            CreatedBy = userId,
            UpdatedBy = userId,
            Version = 1
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(ticket).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "tickets",
            recordId: ticket.TicketId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                ticket.TicketId,
                ticket.RequesterId,
                ticket.TicketCategoryId,
                Priority = priority.Code,
                ticket.Status,
                ticket.AssetId,
                ticket.LocationId,
                ticket.Title
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CreateTicketResponse(
            ticket.TicketId, ticket.Status, priority.Code, ticket.CreatedAt, ticket.Version);
    }
}
