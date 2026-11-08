using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenance;

public sealed record GetMaintenanceQuery(
    int PageNumber,
    int PageSize,
    long? AssetId,
    string? TypeCode) : IQuery<PagedResult<MaintenanceItem>>;
