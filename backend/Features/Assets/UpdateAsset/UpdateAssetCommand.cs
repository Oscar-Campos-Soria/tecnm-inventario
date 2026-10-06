using Mediator;

namespace TecnmInventario.Api.Features.Assets.UpdateAsset;

// InventoryCode NO es editable: es la identidad administrativa estable del
// activo (independiente de ubicación), no un campo de metadatos.
public sealed record UpdateAssetCommand(
    long AssetId,
    int ExpectedVersion,
    long? ManufacturerId,
    long? ModelId,
    long? CurrentLocationId,
    string? SerialNumber,
    string? Hostname,
    string? Notes) : ICommand<UpdateAssetResponse>;
