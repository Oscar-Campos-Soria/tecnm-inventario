using System.Linq.Expressions;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenance;

// Proyección compartida por historial y próximos (se traduce a SQL).
public static class MaintenanceProjection
{
    public static readonly Expression<Func<MaintenanceRecord, MaintenanceItem>> ToItem = r => new MaintenanceItem(
        r.MaintenanceRecordId,
        r.AssetId,
        r.Asset.InventoryCode,
        r.MaintenanceType.Code,
        r.MaintenanceType.Name,
        r.PerformedByNavigation != null ? r.PerformedByNavigation.FirstName + " " + r.PerformedByNavigation.LastName : null,
        r.PerformedAt,
        r.NextMaintenanceAt,
        r.Description,
        r.PartsUsed,
        r.Cost,
        r.IncidentId,
        r.Version,
        r.ScheduledAt);
}
