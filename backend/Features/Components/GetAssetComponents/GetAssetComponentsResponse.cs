namespace TecnmInventario.Api.Features.Components.GetAssetComponents;

public sealed record GetAssetComponentsResponse(
    long AssetComponentId,
    long ComponentTypeId,
    string ComponentTypeCode,
    string ComponentTypeName,
    string? Description,
    string? SerialNumber,
    DateTime InstalledAt,
    DateTime? RemovedAt,
    int Version);
