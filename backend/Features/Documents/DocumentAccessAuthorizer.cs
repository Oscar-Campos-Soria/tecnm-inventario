using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Tickets.Shared;

namespace TecnmInventario.Api.Features.Documents;

public sealed class DocumentAccessAuthorizer : IDocumentAccessAuthorizer
{
    // Ver la evidencia de cualquier incidente: quien los atiende o los consulta.
    private static readonly string[] IncidentStaffPermissions = { "incidents.resolve", "incidents.view" };

    private const string NotFoundMessage = "El documento solicitado no existe.";

    private readonly IRolePermissionService _rolePermissionService;
    private readonly ICurrentUserService _currentUser;
    private readonly TecnmInventarioDbContext _context;

    public DocumentAccessAuthorizer(
        IRolePermissionService rolePermissionService,
        ICurrentUserService currentUser,
        TecnmInventarioDbContext context)
    {
        _rolePermissionService = rolePermissionService;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task EnsureAuthorizedAsync(
        string ownerType,
        long ownerId,
        DocumentOperation operation,
        long? documentCreatedBy,
        CancellationToken cancellationToken)
    {
        var roleCode = _currentUser.RoleCode;
        if (string.IsNullOrEmpty(roleCode))
            throw new ForbiddenAccessException("No se pudo determinar el rol del usuario actual.");

        if (!await HasAnyAsync(roleCode, DocumentAuthorizationMap.GetRequiredPermissions(ownerType), cancellationToken))
            throw new ForbiddenAccessException(
                $"El rol {roleCode} no tiene permiso para acceder a documentos de tipo {ownerType}.");

        switch (ownerType)
        {
            case DocumentOwnerTypes.Ticket:
                await EnsureTicketAccessAsync(roleCode, ownerId, operation, documentCreatedBy, cancellationToken);
                break;
            case DocumentOwnerTypes.Incident:
                await EnsureIncidentAccessAsync(roleCode, ownerId, operation, documentCreatedBy, cancellationToken);
                break;
        }
    }

    private async Task EnsureTicketAccessAsync(
        string roleCode, long ticketId, DocumentOperation operation, long? documentCreatedBy,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var isManager = await _rolePermissionService.HasPermissionAsync(roleCode, TicketAccess.ManagePermission, cancellationToken);

        var ticket = await _context.Tickets.AsNoTracking()
            .Where(t => t.TicketId == ticketId)
            .Select(t => new { t.RequesterId, t.Status })
            .FirstOrDefaultAsync(cancellationToken);

        // Ajeno o inexistente: misma respuesta, no se revela qué tickets existen.
        if (ticket is null || (!isManager && ticket.RequesterId != userId))
            throw new KeyNotFoundException(NotFoundMessage);

        if (operation == DocumentOperation.Upload && TicketStatusCodes.Terminal.Contains(ticket.Status))
            throw new BusinessRuleViolationException("No se pueden adjuntar archivos a un ticket cerrado o cancelado.");

        if (operation == DocumentOperation.Delete && !isManager && documentCreatedBy != userId)
            throw new ForbiddenAccessException("Solo puede eliminar los archivos que usted subió.");
    }

    private async Task EnsureIncidentAccessAsync(
        string roleCode, long incidentId, DocumentOperation operation, long? documentCreatedBy,
        CancellationToken cancellationToken)
    {
        if (await HasAnyAsync(roleCode, IncidentStaffPermissions, cancellationToken))
            return;

        // Solo reporta (p. ej. USUARIO_GENERAL): únicamente la evidencia de sus propios incidentes.
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var reportedBy = await _context.Incidents.AsNoTracking()
            .Where(i => i.IncidentId == incidentId)
            .Select(i => (long?)i.ReportedBy)
            .FirstOrDefaultAsync(cancellationToken);

        if (reportedBy != userId)
            throw new KeyNotFoundException(NotFoundMessage);

        if (operation == DocumentOperation.Delete && documentCreatedBy != userId)
            throw new ForbiddenAccessException("Solo puede eliminar los archivos que usted subió.");
    }

    private async Task<bool> HasAnyAsync(string roleCode, IEnumerable<string> permissions, CancellationToken cancellationToken)
    {
        foreach (var permission in permissions)
        {
            if (await _rolePermissionService.HasPermissionAsync(roleCode, permission, cancellationToken))
                return true;
        }
        return false;
    }
}
