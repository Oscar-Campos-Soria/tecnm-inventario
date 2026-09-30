using Mediator;

namespace TecnmInventario.Api.Features.Auth.Login;

public sealed record LoginCommand(string UsernameOrEmail, string Password)
    : ICommand<LoginResponse>;
