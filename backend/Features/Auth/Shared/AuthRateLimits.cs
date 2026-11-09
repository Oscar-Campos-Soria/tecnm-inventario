namespace TecnmInventario.Api.Features.Auth.Shared;

public static class AuthRateLimits
{
    public const string LoginPolicy = "login";

    // Por IP y por minuto: alcanza para equivocarse varias veces, no para probar contraseñas en masa.
    public const int LoginAttemptsPerMinute = 10;
}
