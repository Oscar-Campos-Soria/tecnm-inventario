using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Profile.ChangePassword;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword)
    : ICommand<ChangePasswordResponse>, ITransactionalCommand;
