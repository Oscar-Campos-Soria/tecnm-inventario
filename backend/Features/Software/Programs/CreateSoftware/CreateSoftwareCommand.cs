using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Programs.CreateSoftware;

public sealed record CreateSoftwareCommand(string Name, string? Vendor, long? SoftwareTypeId)
    : ICommand<CreateSoftwareResponse>, ITransactionalCommand;
