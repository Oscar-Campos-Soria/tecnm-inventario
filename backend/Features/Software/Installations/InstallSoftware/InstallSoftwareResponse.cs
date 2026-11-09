namespace TecnmInventario.Api.Features.Software.Installations.InstallSoftware;

/// <param name="ReplacedAssetSoftwareId">Instalación anterior del mismo programa que dejó de estar vigente.</param>
public sealed record InstallSoftwareResponse(
    long AssetSoftwareId, long AssetId, long SoftwareId, string? VersionInstalled,
    long? ReplacedAssetSoftwareId, int Version);
