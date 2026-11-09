namespace TecnmInventario.Api.Features.Software.Licenses.CreateLicense;

public sealed record CreateLicenseResponse(long LicenseId, long SoftwareId, int Version);
