namespace TecnmInventario.Api.Features.Software.Licenses.GetLicenseById;

/// <param name="MaskedLicenseKey">Solo los últimos 4 caracteres; la clave completa no sale por la API.</param>
public sealed record GetLicenseByIdResponse(
    long LicenseId,
    long SoftwareId,
    string SoftwareName,
    string? SoftwareVendor,
    long LicenseTypeId,
    string LicenseTypeCode,
    string LicenseTypeName,
    string? MaskedLicenseKey,
    int? QuantityPurchased,
    int InUse,
    int? Available,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool IsExpired,
    long? SupplierId,
    string? SupplierName,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version);
