using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;

namespace TecnmInventario.Api.Features.Disposals.CreateDisposal;

/// <summary>
/// Baja formal: única vía para llevar un activo a DADO_DE_BAJA (el cambio
/// manual de estado ya no la acepta). El activo permanece en assets.
/// </summary>
public sealed class CreateDisposalHandler : ICommandHandler<CreateDisposalCommand, CreateDisposalResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _assetStatusChanger;

    public CreateDisposalHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IAssetStatusChanger assetStatusChanger)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _assetStatusChanger = assetStatusChanger;
    }

    public async ValueTask<CreateDisposalResponse> Handle(
        CreateDisposalCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El activo solicitado no existe.");

        if (asset.AssetStatus.Code == AssetStatusCodes.DadoDeBaja
            || await _context.AssetDisposals.AnyAsync(d => d.AssetId == asset.AssetId, cancellationToken))
        {
            throw new BusinessRuleViolationException("El activo ya fue dado de baja.");
        }

        var openCodes = IncidentStatusCodes.Open.ToArray();
        var openIncidents = await _context.Incidents.CountAsync(
            i => i.AssetId == asset.AssetId && openCodes.Contains(i.IncidentStatus.Code), cancellationToken);
        if (openIncidents > 0)
        {
            throw new BusinessRuleViolationException(
                $"El activo tiene {openIncidents} incidente(s) abierto(s); resuélvalos antes de darlo de baja.");
        }

        if (await _context.AssetAssignments.AnyAsync(a => a.AssetId == asset.AssetId && a.ReturnedAt == null, cancellationToken))
        {
            throw new BusinessRuleViolationException(
                "El activo tiene una asignación activa; regístrese la devolución antes de darlo de baja.");
        }

        var reasonCode = await _context.DisposalReasons
            .Where(r => r.DisposalReasonId == command.DisposalReasonId && !r.IsDeleted)
            .Select(r => r.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleViolationException("El motivo de baja especificado no existe.");

        var folio = command.Folio.Trim();
        if (await _context.AssetDisposals.AnyAsync(d => d.Folio == folio, cancellationToken))
            throw new BusinessRuleViolationException($"Ya existe una baja con el folio {folio}.");

        // El activo se valida contra la versión que vio el usuario (409 si cambió).
        // Si cambia entre esta lectura y el UPDATE, el token de concurrencia de
        // Version hace fallar el cambio de estado con el mismo 409.
        if (asset.Version != command.ExpectedVersion)
            throw new DbUpdateConcurrencyException("El activo fue modificado por otra persona.");

        // Transacción explícita: acta + estado del activo + audit_log, todo o nada.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var disposal = new AssetDisposal
        {
            AssetId = asset.AssetId,
            DisposalReasonId = command.DisposalReasonId,
            Folio = folio,
            Dictamen = string.IsNullOrWhiteSpace(command.Dictamen) ? null : command.Dictamen.Trim(),
            DisposedAt = command.DisposedAt ?? DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedBy = userId,
            UpdatedBy = userId,
            Version = 1
        };
        _context.AssetDisposals.Add(disposal);

        // ChangeAsync guarda todo lo pendiente (la baja incluida) junto con el estado.
        var change = await _assetStatusChanger.ChangeAsync(
            asset, AssetStatusCodes.DadoDeBaja, $"Baja formal, folio {folio}", cancellationToken);

        await _auditLog.LogAsync(
            tableName: "asset_disposals",
            recordId: disposal.AssetDisposalId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                disposal.AssetDisposalId,
                disposal.AssetId,
                Reason = reasonCode,
                disposal.Folio,
                disposal.DisposedAt,
                disposal.Dictamen
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CreateDisposalResponse(
            disposal.AssetDisposalId, asset.AssetId, disposal.Folio, disposal.DisposedAt,
            change.NewStatusCode, change.NewVersion);
    }
}
