namespace TecnmInventario.Api.Features.Software.Installations.GetAssetSoftware;

public sealed record GetAssetSoftwareResponse(
    long AssetSoftwareId,
    long SoftwareId,
    string SoftwareName,
    string? SoftwareVendor,
    string? VersionInstalled,
    DateTime InstalledAt,
    bool IsCurrent,
    bool HasActiveLicense,
    int Version);
