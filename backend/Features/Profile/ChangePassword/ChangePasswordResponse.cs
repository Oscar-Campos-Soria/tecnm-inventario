namespace TecnmInventario.Api.Features.Profile.ChangePassword;

/// <param name="OtherSessionsClosed">Sesiones en otros navegadores o equipos que se cerraron.</param>
public sealed record ChangePasswordResponse(int OtherSessionsClosed);
