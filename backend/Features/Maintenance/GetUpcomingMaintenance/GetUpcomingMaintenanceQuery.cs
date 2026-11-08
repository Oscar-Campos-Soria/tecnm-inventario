using Mediator;
using TecnmInventario.Api.Features.Maintenance.GetMaintenance;

namespace TecnmInventario.Api.Features.Maintenance.GetUpcomingMaintenance;

/// <param name="Days">Ventana hacia adelante; los vencidos siempre se incluyen.</param>
public sealed record GetUpcomingMaintenanceQuery(int Days) : IQuery<IReadOnlyList<MaintenanceItem>>;
