using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Components.Shared;

internal static class ComponentRules
{
    public static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Activo existente y en servicio: a un activo dado de baja no se le cambia hardware.</summary>
    public static async Task EnsureAssetInServiceAsync(TecnmInventarioDbContext context, long assetId, CancellationToken ct)
    {
        var status = await context.Assets
            .Where(a => a.AssetId == assetId && !a.IsDeleted)
            .Select(a => a.AssetStatus.Code)
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("El activo solicitado no existe.");

        if (status == AssetStatusCodes.DadoDeBaja)
        {
            throw new BusinessRuleViolationException("El activo está dado de baja; no se pueden cambiar sus componentes.");
        }
    }

    /// <summary>Componente del activo indicado, con la versión que vio el cliente (409 si cambió).</summary>
    public static async Task<AssetComponent> LoadAsync(
        TecnmInventarioDbContext context, long assetId, long componentId, int expectedVersion, CancellationToken ct)
    {
        var component = await context.AssetComponents
            .FirstOrDefaultAsync(c => c.AssetComponentId == componentId && c.AssetId == assetId, ct)
            ?? throw new KeyNotFoundException("El componente solicitado no existe en este equipo.");

        if (component.Version != expectedVersion)
        {
            throw new DbUpdateConcurrencyException("El componente fue modificado por otro usuario mientras tanto.");
        }

        return component;
    }

    public static object Snapshot(AssetComponent c) => new
    {
        c.AssetId,
        c.ComponentTypeId,
        c.Description,
        c.SerialNumber,
        c.InstalledAt,
        c.RemovedAt,
        c.Version
    };
}
