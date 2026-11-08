using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Inventory.GetInventorySessionById;

public sealed record InventoryRecordItem(
    long InventoryRecordId,
    long AssetId,
    string InventoryCode,
    string AssetTypeName,
    // Estado y versión actuales del activo: la conciliación los necesita
    // (mover exige DISPONIBLE/ASIGNADO y expectedVersion).
    string AssetStatusCode,
    int AssetVersion,
    long? AssetCurrentLocationId,
    long? ExpectedLocationId,
    string? ExpectedLocationName,
    long? LocationFoundId,
    string? LocationFoundName,
    string Status,
    DateTime? CheckedAt,
    string? CheckedByUsername,
    string? Notes,
    string? Adjustment,
    long? IncidentId,
    int Version);
