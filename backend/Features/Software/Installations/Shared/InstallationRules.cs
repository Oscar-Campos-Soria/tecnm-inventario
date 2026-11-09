using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Software.Installations.Shared;

internal static class InstallationRules
{
    /// <summary>
    /// Bloquea la fila del activo hasta el fin de la transacción: dos instalaciones
    /// simultáneas del mismo programa no deben dejar dos filas vigentes
    /// (asset_software no tiene índice único que lo impida).
    /// </summary>
    public static async Task LockAssetAsync(TecnmInventarioDbContext context, long assetId, CancellationToken ct)
    {
        // InMemory (tests rápidos) no tiene bloqueos de fila.
        if (!context.Database.IsRelational())
        {
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM inventario.assets WHERE asset_id = {assetId} FOR UPDATE", ct);
    }

    public static object Snapshot(AssetSoftware i) => new
    {
        i.AssetId,
        i.SoftwareId,
        i.VersionInstalled,
        i.InstalledAt,
        i.IsCurrent,
        i.Version
    };
}
