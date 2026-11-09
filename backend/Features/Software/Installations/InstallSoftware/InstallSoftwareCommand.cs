using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Software.Installations.InstallSoftware;

/// <param name="InstalledAt">Opcional; por defecto, ahora.</param>
public sealed record InstallSoftwareCommand(
    long AssetId, long SoftwareId, string? VersionInstalled, DateTime? InstalledAt)
    : ICommand<InstallSoftwareResponse>, ITransactionalCommand;
