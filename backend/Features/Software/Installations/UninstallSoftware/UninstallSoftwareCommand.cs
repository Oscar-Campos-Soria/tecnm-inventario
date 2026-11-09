using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Installations.UninstallSoftware;

public sealed record UninstallSoftwareCommand(long AssetId, long AssetSoftwareId, int ExpectedVersion)
    : ICommand<UninstallSoftwareResponse>, ITransactionalCommand;
