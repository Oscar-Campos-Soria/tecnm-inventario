using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Users.ResetUserPassword;

public sealed record ResetUserPasswordCommand(long UserId, int ExpectedVersion, string NewPassword)
    : ICommand<ResetUserPasswordResponse>, ITransactionalCommand;
