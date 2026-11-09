using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Licenses.CreateLicense;

/// <param name="QuantityPurchased">Null = sin límite (freeware, licencia de sitio); 0 = sin cupo.</param>
public sealed record CreateLicenseCommand(
    long SoftwareId,
    long LicenseTypeId,
    string? LicenseKey,
    int? QuantityPurchased,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    long? SupplierId,
    string? Notes) : ICommand<CreateLicenseResponse>, ITransactionalCommand;
