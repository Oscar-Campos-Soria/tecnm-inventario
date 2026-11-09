namespace TecnmInventario.Api.Features.Reports.Shared;

/// <summary>
/// Reporte ya calculado, independiente del formato: los renderizadores lo
/// convierten a Excel o PDF. Las celdas son texto, número, fecha o null.
/// </summary>
public sealed record ReportTable(
    string Title,
    string FileSlug,
    IReadOnlyList<string> Filters,
    IReadOnlyList<string> Columns,
    IReadOnlyList<object?[]> Rows,
    bool Truncated);

public static class ReportCodes
{
    public const string AssetsByLocation = "assets-by-location";
    public const string Assets = "assets";
    public const string OpenIncidents = "open-incidents";
    public const string Maintenance = "maintenance";
    public const string Licenses = "licenses";

    public static readonly IReadOnlyCollection<string> All =
        new[] { AssetsByLocation, Assets, OpenIncidents, Maintenance, Licenses };

    public const string Xlsx = "xlsx";
    public const string Pdf = "pdf";

    // Tope por reporte: arriba de esto conviene filtrar (o un proceso por lotes).
    public const int MaxRows = 10_000;
}
