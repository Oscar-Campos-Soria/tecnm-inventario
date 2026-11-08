using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.GetInventorySessions;

public sealed record GetInventorySessionsQuery(int PageNumber, int PageSize) : IQuery<PagedResult<InventorySessionSummary>>;
