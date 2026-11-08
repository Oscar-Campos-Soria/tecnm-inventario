using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Tickets.Shared;

/// <summary>
/// Regla de visibilidad compartida por los casos de uso de Tickets:
/// con tickets.manage se ve todo; sin él, solo los tickets propios.
/// Un ticket ajeno responde 404 (igual que uno inexistente) para no
/// revelar qué tickets existen.
/// </summary>
public static class TicketAccess
{
    public const string ManagePermission = "tickets.manage";

    public const string NotFoundMessage = "El ticket solicitado no existe.";

    public static async Task<bool> IsManagerAsync(
        IRolePermissionService rolePermissions,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        var roleCode = currentUser.RoleCode;
        return !string.IsNullOrEmpty(roleCode)
            && await rolePermissions.HasPermissionAsync(roleCode, ManagePermission, cancellationToken);
    }

    /// <summary>Carga (con tracking) el ticket si el usuario puede verlo; si no, 404.</summary>
    public static async Task<Ticket> GetVisibleAsync(
        TecnmInventarioDbContext context,
        long ticketId,
        long userId,
        bool isManager,
        CancellationToken cancellationToken)
    {
        var ticket = await context.Tickets
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, cancellationToken);

        if (ticket is null || (!isManager && ticket.RequesterId != userId))
            throw new KeyNotFoundException(NotFoundMessage);

        return ticket;
    }
}
