namespace TecnmInventario.Api.Features.Assets.GetMyAssets;

/// <summary>Tarjeta de "Mis equipos": datos básicos, la asignación y los tickets que el usuario levantó sobre ese equipo.</summary>
public sealed record MyAssetResponse(
    long AssetId,
    string InventoryCode,
    string AssetTypeName,
    string? ManufacturerName,
    string? ModelName,
    string? SerialNumber,
    string StatusCode,
    string StatusName,
    string? LocationName,
    string AssignmentTypeName,
    DateTime AssignedAt,
    IReadOnlyList<MyAssetTicket> Tickets);

public sealed record MyAssetTicket(long TicketId, string Title, string Status, DateTime CreatedAt);
