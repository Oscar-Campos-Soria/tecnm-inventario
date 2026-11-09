using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Licenses.UpdateLicense;

/// <param name="LicenseKey">Null = conservar la clave actual; texto vacío = quitarla.</param>
public sealed record UpdateLicenseCommand(
    long LicenseId,
    int ExpectedVersion,
    long LicenseTypeId,
    string? LicenseKey,
    int? QuantityPurchased,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    long? SupplierId,
    string? Notes) : ICommand<UpdateLicenseResponse>, ITransactionalCommand;
