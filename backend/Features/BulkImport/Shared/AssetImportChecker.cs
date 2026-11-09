using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.BulkImport.Shared;

public sealed record CheckedRow(
    int RowNumber,
    string? InventoryCode,
    string? TypeName,
    string? LocationName,
    IReadOnlyList<string> Errors,
    long AssetTypeId,
    long? ManufacturerId,
    long? ModelId,
    long? LocationId,
    string? SerialNumber,
    string? Hostname,
    string? Notes)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Mismas reglas que el alta de un activo (CreateAssetValidator) aplicadas a todo el
/// archivo, más códigos repetidos dentro del archivo. Los catálogos se cargan una
/// vez (no una consulta por fila).
/// </summary>
public sealed class AssetImportChecker
{
    private readonly TecnmInventarioDbContext _context;

    public AssetImportChecker(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CheckedRow>> CheckAsync(IReadOnlyList<ImportRow> rows, CancellationToken ct)
    {
        var types = await _context.AssetTypes.AsNoTracking().Where(t => !t.IsDeleted)
            .Select(t => new { t.AssetTypeId, t.Code, t.Name }).ToListAsync(ct);
        var manufacturers = await _context.Manufacturers.AsNoTracking().Where(m => !m.IsDeleted)
            .Select(m => new { m.ManufacturerId, m.Name }).ToListAsync(ct);
        var models = await _context.Models.AsNoTracking().Where(m => !m.IsDeleted)
            .Select(m => new { m.ModelId, m.ModelName, m.ManufacturerId }).ToListAsync(ct);
        var locations = await _context.Locations.AsNoTracking()
            .Select(l => new { l.LocationId, l.Code, l.Name }).ToListAsync(ct);

        var codes = rows.Select(r => r[AssetImportParser.Code]).Where(c => c != null).Cast<string>().Distinct().ToList();
        // IgnoreQueryFilters no aplica (assets no tiene filtro): el código es único aun entre borrados.
        var taken = (await _context.Assets.AsNoTracking().Where(a => codes.Contains(a.InventoryCode))
            .Select(a => a.InventoryCode).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repeated = rows.GroupBy(r => r[AssetImportParser.Code] ?? "", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key != "" && g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        static bool Same(string a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        return rows.Select(row =>
        {
            var errors = new List<string>();
            var code = row[AssetImportParser.Code];
            if (code is null) errors.Add("Falta el código de inventario.");
            else if (code.Length > DbFieldLengths.Assets.InventoryCode) errors.Add($"El código pasa de {DbFieldLengths.Assets.InventoryCode} caracteres.");
            else if (taken.Contains(code)) errors.Add("Ese código de inventario ya está en uso.");
            else if (repeated.Contains(code)) errors.Add("El código se repite en el archivo.");

            var typeText = row[AssetImportParser.Type];
            var type = typeText is null ? null : types.FirstOrDefault(t => Same(t.Code, typeText) || Same(t.Name, typeText));
            if (typeText is null) errors.Add("Falta el tipo de activo.");
            else if (type is null) errors.Add($"El tipo \"{typeText}\" no existe (usa el código o el nombre de la hoja Catálogos).");

            var manufacturerText = row[AssetImportParser.Manufacturer];
            var manufacturer = manufacturerText is null ? null : manufacturers.FirstOrDefault(m => Same(m.Name, manufacturerText));
            if (manufacturerText is not null && manufacturer is null) errors.Add($"El fabricante \"{manufacturerText}\" no existe.");

            var modelText = row[AssetImportParser.Model];
            long? modelId = null;
            if (modelText is not null)
            {
                var candidates = models.Where(m => Same(m.ModelName, modelText)
                    && (manufacturer is null || m.ManufacturerId == manufacturer.ManufacturerId)).ToList();
                if (candidates.Count == 1) modelId = candidates[0].ModelId;
                else if (candidates.Count == 0) errors.Add($"El modelo \"{modelText}\" no existe{(manufacturer is null ? "" : " para ese fabricante")}.");
                else errors.Add($"Hay varios modelos \"{modelText}\"; indica también el fabricante.");
            }

            var locationText = row[AssetImportParser.Location];
            var location = locationText is null ? null : locations.FirstOrDefault(l => Same(l.Code, locationText));
            if (locationText is not null && location is null) errors.Add($"La ubicación \"{locationText}\" no existe (usa su código).");

            var serial = row[AssetImportParser.Serial];
            if (serial?.Length > DbFieldLengths.Assets.SerialNumber) errors.Add($"La serie pasa de {DbFieldLengths.Assets.SerialNumber} caracteres.");
            var hostname = row[AssetImportParser.Hostname];
            if (hostname?.Length > DbFieldLengths.Assets.Hostname) errors.Add($"El hostname pasa de {DbFieldLengths.Assets.Hostname} caracteres.");

            return new CheckedRow(row.RowNumber, code, type?.Name, location?.Name, errors,
                type?.AssetTypeId ?? 0, manufacturer?.ManufacturerId, modelId, location?.LocationId,
                serial, hostname, row[AssetImportParser.Notes]);
        }).ToList();
    }
}
