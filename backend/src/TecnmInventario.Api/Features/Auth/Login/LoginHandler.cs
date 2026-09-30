using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Infrastructure;

namespace TecnmInventario.Api.Features.Auth.Login;

public sealed class LoginHandler : ICommandHandler<LoginCommand, LoginResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public LoginHandler(TecnmInventarioDbContext context, IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _tokenGenerator = tokenGenerator;
    }

    public async ValueTask<LoginResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await _context.AppUsers
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u => !u.IsDeleted &&
                     (u.Username == command.UsernameOrEmail || u.Email == command.UsernameOrEmail),
                cancellationToken);

        if (user is null || user.Status != "activo")
        {
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
        if (!passwordValid)
        {
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        var (token, expiresAt) = _tokenGenerator.GenerateToken(user);

        return new LoginResponse(token, expiresAt, user.UserId, user.Username, user.Role.Code);
    }
}
