using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Software.Programs.Shared;

internal static class SoftwareNames
{
    public static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // IgnoreQueryFilters: el UNIQUE (name, vendor) de la BD incluye filas borradas.
    // Un fabricante vacío también cuenta como duplicado (en la BD NULL no choca).
    public static async Task<bool> IsFreeAsync(
        TecnmInventarioDbContext context, string name, string? vendor, long? exceptId, CancellationToken ct)
    {
        var trimmedName = name.Trim();
        var trimmedVendor = NullIfBlank(vendor);
        return !await context.Softwares
            .IgnoreQueryFilters()
            .AnyAsync(s => s.Name == trimmedName && s.Vendor == trimmedVendor
                && (exceptId == null || s.SoftwareId != exceptId), ct);
    }
}
