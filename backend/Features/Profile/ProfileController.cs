using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TecnmInventario.Api.Features.Auth.Shared;
using TecnmInventario.Api.Features.Profile.ChangePassword;
using TecnmInventario.Api.Features.Profile.GetProfile;
using TecnmInventario.Api.Features.Profile.UnlinkExternalLogin;

namespace TecnmInventario.Api.Features.Profile;

// Cuenta propia: basta con haber iniciado sesión (política por defecto).
// Bajo /api/auth porque la cookie de refresh solo viaja a esa ruta (Path=/api/auth):
// cambiar la contraseña necesita saber cuál es la sesión actual para no cerrarla.
[ApiController]
[Route("api/auth/profile")]
public class ProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProfileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<GetProfileResponse>> Get(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetProfileQuery(), cancellationToken));

    // Mismo freno que el login: la contraseña actual no se puede adivinar desde aquí.
    [HttpPost("password")]
    [EnableRateLimiting(AuthRateLimits.LoginPolicy)]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword(
        ChangePasswordCommand command, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command, cancellationToken));

    [HttpDelete("external-logins/{provider}")]
    public async Task<IActionResult> Unlink(string provider, CancellationToken cancellationToken)
    {
        await _mediator.Send(new UnlinkExternalLoginCommand(provider), cancellationToken);
        return NoContent();
    }
}
