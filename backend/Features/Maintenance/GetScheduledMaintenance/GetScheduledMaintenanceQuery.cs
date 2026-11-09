using Mediator;
using TecnmInventario.Api.Features.Maintenance.GetMaintenance;

namespace TecnmInventario.Api.Features.Maintenance.GetScheduledMaintenance;

/// <param name="Days">Ventana hacia adelante; los atrasados siempre salen.</param>
public sealed record GetScheduledMaintenanceQuery(int Days) : IQuery<IReadOnlyList<MaintenanceItem>>;
