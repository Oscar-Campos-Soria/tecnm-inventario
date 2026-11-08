using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Locations.Shared;

namespace TecnmInventario.Api.Features.Inventory.CreateInventorySession;

/// <summary>
/// Abre un conteo: la lista esperada son los activos (no dados de baja) cuya
/// ubicación actual está en el área elegida o en sus sub-ubicaciones.
/// </summary>
public sealed class CreateInventorySessionHandler
    : ICommandHandler<CreateInventorySessionCommand, CreateInventorySessionResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CreateInventorySessionHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CreateInventorySessionResponse> Handle(
        CreateInventorySessionCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var locationIds = await LocationTree.DescendantsAndSelfAsync(_context, command.LocationId, cancellationToken);
        if (locationIds.Count == 0)
            throw new BusinessRuleViolationException("La ubicación especificada no existe.");

        // Dos conteos abiertos sobre la misma área se pisarían.
        var busy = await _context.InventorySessions.AnyAsync(
            s => s.Status == InventorySessionStatusCodes.Abierta && s.LocationId == command.LocationId, cancellationToken);
        if (busy)
            throw new BusinessRuleViolationException("Ya hay un conteo abierto para esa ubicación; ciérralo antes de abrir otro.");

        var expected = await _context.Assets
            .Where(a => !a.IsDeleted
                && a.AssetStatus.Code != AssetStatusCodes.DadoDeBaja
                && a.CurrentLocationId != null
                && locationIds.Contains(a.CurrentLocationId.Value))
            .Select(a => new { a.AssetId, a.CurrentLocationId })
            .ToListAsync(cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var session = new InventorySession
        {
            Name = command.Name.Trim(),
            LocationId = command.LocationId,
            Status = InventorySessionStatusCodes.Abierta,
            StartedAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
            Version = 1
        };
        _context.InventorySessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var asset in expected)
        {
            _context.InventoryRecords.Add(new InventoryRecord
            {
                InventorySessionId = session.InventorySessionId,
                AssetId = asset.AssetId,
                ExpectedLocationId = asset.CurrentLocationId,
                Found = false,
                CreatedBy = userId,
                UpdatedBy = userId,
                Version = 1
            });
        }
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "inventory_sessions",
            recordId: session.InventorySessionId,
            action: "INSERT",
            oldData: null,
            newData: new { session.InventorySessionId, session.Name, session.LocationId, ExpectedAssets = expected.Count },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CreateInventorySessionResponse(session.InventorySessionId, session.Name, expected.Count, session.Version);
    }
}
