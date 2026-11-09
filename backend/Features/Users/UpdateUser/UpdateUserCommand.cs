using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Users.UpdateUser;

public sealed record UpdateUserCommand(
    long UserId,
    int ExpectedVersion,
    string Email,
    string FirstName,
    string LastName,
    string RoleCode) : ICommand<UpdateUserResponse>, ITransactionalCommand;
