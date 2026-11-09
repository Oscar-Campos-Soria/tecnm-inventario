namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenses;

/// <param name="QuantityPurchased">Null = sin límite de instalaciones.</param>
/// <param name="Available">Null cuando la licencia no tiene límite.</param>
public sealed record GetLicensesResponse(
    long LicenseId,
    long SoftwareId,
    string SoftwareName,
    string? SoftwareVendor,
    string LicenseTypeCode,
    string LicenseTypeName,
    int? QuantityPurchased,
    int InUse,
    int? Available,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool IsExpired,
    string? SupplierName,
    int Version);
