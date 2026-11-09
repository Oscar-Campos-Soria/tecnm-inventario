using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Users.ChangeUserStatus;

public sealed record ChangeUserStatusCommand(long UserId, int ExpectedVersion, string Status)
    : ICommand<ChangeUserStatusResponse>, ITransactionalCommand;
