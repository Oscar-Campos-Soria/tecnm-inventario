namespace TecnmInventario.Api.Features.Dashboard.GetSummary;

public sealed record CountItem(string Code, string Name, int Count);

/// <param name="TotalAssets">Activos en servicio (sin los dados de baja).</param>
/// <param name="AssetsByStatus">Todos los estados, incluido DADO_DE_BAJA.</param>
/// <param name="AssetsByType">Solo activos en servicio.</param>
/// <param name="AssetsByLocation">Las 10 ubicaciones con más activos en servicio.</param>
/// <param name="WindowDays">Ventana de "por vencer" para preventivos y licencias.</param>
public sealed record GetDashboardSummaryResponse(
    int TotalAssets,
    IReadOnlyList<CountItem> AssetsByStatus,
    IReadOnlyList<CountItem> AssetsByType,
    IReadOnlyList<CountItem> OpenIncidentsByPriority,
    int PreventiveOverdue,
    int PreventiveDueSoon,
    IReadOnlyList<CountItem> AssetsByLocation,
    int LicensesExpired,
    int LicensesExpiringSoon,
    int WindowDays,
    DateTime GeneratedAt);
