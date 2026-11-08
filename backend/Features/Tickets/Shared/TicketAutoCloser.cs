using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Tickets.Shared;

/// <summary>
/// Cierra los tickets RESUELTO cuyo plazo de confirmación ya venció.
/// Separado de TicketAutoCloseService para poder probarlo sin el temporizador.
/// Todo queda firmado por la cuenta "system" (database.sql).
/// </summary>
public static class TicketAutoCloser
{
    public const string SystemUsername = "system";

    // Lote por pasada: si hubiera más, la siguiente pasada (1 h) sigue con el resto.
    private const int BatchSize = 200;

    public static async Task<int> CloseExpiredAsync(
        TecnmInventarioDbContext context,
        int autoCloseDays,
        DateTime nowUtc,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var systemUserId = await context.AppUsers
            .Where(u => u.Username == SystemUsername)
            .Select(u => (long?)u.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (systemUserId is null)
        {
            logger.LogWarning("Cierre automático de tickets omitido: no existe la cuenta '{Username}'.", SystemUsername);
            return 0;
        }

        var cutoff = nowUtc.AddDays(-autoCloseDays);
        var expiredIds = await context.Tickets.AsNoTracking()
            .Where(t => t.Status == TicketStatusCodes.Resuelto && t.ResolvedAt != null && t.ResolvedAt <= cutoff)
            .OrderBy(t => t.ResolvedAt)
            .Select(t => t.TicketId)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var auditLog = new AuditLogService(context, new SystemCurrentUser(systemUserId.Value));
        var closed = 0;

        foreach (var ticketId in expiredIds)
        {
            context.ChangeTracker.Clear();

            // Se relee: el usuario pudo reabrirlo o confirmarlo entre la consulta y ahora.
            var ticket = await context.Tickets.FirstOrDefaultAsync(t => t.TicketId == ticketId, cancellationToken);
            if (ticket is null || ticket.Status != TicketStatusCodes.Resuelto || ticket.ResolvedAt > cutoff)
                continue;

            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

                ticket.Status = TicketStatusCodes.Cerrado;
                ticket.ClosedAt = nowUtc;
                ticket.UpdatedBy = systemUserId;

                var comment = new TicketComment
                {
                    TicketId = ticket.TicketId,
                    AuthorId = systemUserId.Value,
                    Body = $"Cerrado automáticamente: no hubo respuesta en {autoCloseDays} días después de resolverse.",
                    IsInternal = false,
                    CreatedAt = nowUtc
                };
                context.TicketComments.Add(comment);

                await context.SaveChangesAsync(cancellationToken);

                await auditLog.LogAsync(
                    tableName: "tickets",
                    recordId: ticket.TicketId,
                    action: "UPDATE",
                    oldData: new { Status = TicketStatusCodes.Resuelto },
                    newData: new { ticket.Status, ticket.ClosedAt, Reason = "Cierre automático" },
                    cancellationToken);

                await auditLog.LogAsync(
                    tableName: "ticket_comments",
                    recordId: comment.TicketCommentId,
                    action: "INSERT",
                    oldData: null,
                    newData: new { comment.TicketCommentId, comment.TicketId, comment.AuthorId, comment.IsInternal },
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                closed++;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Alguien lo movió en el mismo instante: se respeta su cambio.
                logger.LogInformation("Ticket {TicketId} cambió durante el cierre automático; se omite.", ticketId);
            }
        }

        context.ChangeTracker.Clear();
        return closed;
    }

    private sealed class SystemCurrentUser(long userId) : ICurrentUserService
    {
        public long? UserId => userId;
        public string? Username => SystemUsername;
        public string? RoleCode => null;
        public System.Net.IPAddress? IpAddress => null;
    }
}
