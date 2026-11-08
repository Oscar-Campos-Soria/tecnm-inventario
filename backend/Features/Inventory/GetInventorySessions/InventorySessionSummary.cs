using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.GetInventorySessions;

public sealed record InventorySessionSummary(
    long InventorySessionId,
    string Name,
    string? LocationName,
    string Status,
    DateTime StartedAt,
    DateTime? EndedAt,
    int TotalRecords,
    int CheckedRecords,
    int FoundRecords,
    string? CreatedByUsername);
