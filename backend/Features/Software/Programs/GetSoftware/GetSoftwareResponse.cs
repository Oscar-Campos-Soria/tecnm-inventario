namespace TecnmInventario.Api.Features.Software.Programs.GetSoftware;

public sealed record GetSoftwareResponse(
    long SoftwareId,
    string Name,
    string? Vendor,
    long? SoftwareTypeId,
    string? SoftwareTypeName,
    int LicenseCount,
    int InstallCount,
    int Version);
