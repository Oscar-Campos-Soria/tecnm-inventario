using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Licenses.EndLicenseAssignment;

public sealed record EndLicenseAssignmentCommand(long LicenseAssignmentId, int ExpectedVersion)
    : ICommand<EndLicenseAssignmentResponse>, ITransactionalCommand;
