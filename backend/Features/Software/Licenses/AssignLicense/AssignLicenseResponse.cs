namespace TecnmInventario.Api.Features.Software.Licenses.AssignLicense;

public sealed record AssignLicenseResponse(
    long LicenseAssignmentId, long LicenseId, long AssetId, int InUse, int? QuantityPurchased, int Version);
