using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Licenses.AssignLicense;

public sealed record AssignLicenseCommand(long LicenseId, long AssetId)
    : ICommand<AssignLicenseResponse>, ITransactionalCommand;
