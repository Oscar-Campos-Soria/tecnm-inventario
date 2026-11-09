using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Licenses.DeleteLicense;

public sealed record DeleteLicenseCommand(long LicenseId, int ExpectedVersion)
    : ICommand<DeleteLicenseResponse>, ITransactionalCommand;
