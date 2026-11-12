namespace TecnmInventario.Api.Features.Assets.GetAssets;

public sealed record GetAssetsResponse(
    long AssetId,
    string InventoryCode,
    string? SerialNumber,
    string? Hostname,
    int Version,
    string AssetTypeName,
    string StatusCode,
    string StatusName,
    long? LocationId,
    string? LocationName,
    string? ManufacturerName = null,
    string? ModelName = null);
