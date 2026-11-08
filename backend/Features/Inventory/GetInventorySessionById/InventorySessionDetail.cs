using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.GetInventorySessionById;

public sealed record InventorySessionDetail(
    long InventorySessionId,
    string Name,
    long? LocationId,
    string? LocationName,
    string Status,
    DateTime StartedAt,
    DateTime? EndedAt,
    IReadOnlyDictionary<string, int> Summary,
    IReadOnlyList<InventoryRecordItem> Records,
    int Version);
