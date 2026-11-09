using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Software.Licenses.Shared;

internal static class LicenseRules
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Bloquea la fila de la licencia hasta el fin de la transacción (la abre
    /// TransactionBehavior). Serializa asignaciones, cambios de cantidad y bajas
    /// de la misma licencia: sin esto, dos asignaciones simultáneas podrían
    /// ocupar el mismo último lugar.
    /// </summary>
    public static async Task LockAsync(TecnmInventarioDbContext context, long licenseId, CancellationToken ct)
    {
        // InMemory (tests rápidos) no tiene bloqueos de fila; el caso real se prueba con Testcontainers.
        if (!context.Database.IsRelational())
        {
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM inventario.software_licenses WHERE license_id = {licenseId} FOR UPDATE", ct);
    }

    public static Task<int> CountInUseAsync(TecnmInventarioDbContext context, long licenseId, CancellationToken ct)
        => context.SoftwareLicenseAssignments.CountAsync(a => a.LicenseId == licenseId && a.EndedAt == null, ct);

    /// <summary>Snapshot de auditoría: nunca incluye la clave (regla 16), solo si existe.</summary>
    public static object Snapshot(SoftwareLicense l) => new
    {
        l.SoftwareId,
        l.LicenseTypeId,
        HasLicenseKey = l.LicenseKey != null,
        l.QuantityPurchased,
        l.ValidFrom,
        l.ValidUntil,
        l.SupplierId,
        l.Notes,
        l.Version
    };

    public static object Snapshot(SoftwareLicenseAssignment a) => new
    {
        a.LicenseId,
        a.AssetId,
        a.AssignedAt,
        a.EndedAt,
        a.Version
    };

    /// <summary>La clave solo se muestra enmascarada; nunca sale completa por la API.</summary>
    public static string? Mask(string? key)
        => key is null ? null : key.Length > 4 ? $"••••{key[^4..]}" : "••••";
}
