using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Programs.DeleteSoftware;

public sealed record DeleteSoftwareCommand(long SoftwareId, int ExpectedVersion)
    : ICommand<DeleteSoftwareResponse>, ITransactionalCommand;
