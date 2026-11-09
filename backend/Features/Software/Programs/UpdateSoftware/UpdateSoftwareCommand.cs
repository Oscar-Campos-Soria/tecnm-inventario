using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Programs.UpdateSoftware;

public sealed record UpdateSoftwareCommand(
    long SoftwareId, int ExpectedVersion, string Name, string? Vendor, long? SoftwareTypeId)
    : ICommand<UpdateSoftwareResponse>, ITransactionalCommand;
