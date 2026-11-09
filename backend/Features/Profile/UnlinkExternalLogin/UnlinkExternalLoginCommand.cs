using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Profile.UnlinkExternalLogin;

public sealed record UnlinkExternalLoginCommand(string Provider) : ICommand, ITransactionalCommand;
