using System.Text.Json;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Auth.Shared;
using TecnmInventario.Api.Features.Profile.ChangePassword;
using TecnmInventario.Api.Features.Profile.GetProfile;
using TecnmInventario.Api.Features.Profile.UnlinkExternalLogin;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Cuenta propia: cambiar contraseña (verifica la actual, cierra las otras
/// sesiones y conserva la actual, sin hash en audit_log) y desvincular un
/// proveedor externo. Proveedor en memoria.
/// </summary>
public class ProfileTests : IDisposable
{
    private const string RawCurrentToken = "token-de-esta-sesion";

    private readonly AssetFlowsHarness _h = AssetFlowsHarness.CreateInMemory();
    private readonly RecordingAuditLogService _audit = new();
    private readonly FakeCurrentUser _user = new();
    private readonly Guid _currentFamily = Guid.NewGuid();
    private readonly Guid _otherFamily = Guid.NewGuid();

    private sealed class FakeCookies : IAuthCookieWriter
    {
        public void Write(string accessToken, DateTime accessExpiresAt, string refreshToken, DateTime refreshExpiresAt) { }
        public void Clear() { }
        public string? ReadRefreshToken() => RawCurrentToken;
    }

    public ProfileTests()
    {
        var now = DateTime.UtcNow;
        _h.Context.Roles.Add(new Role { RoleId = 1, Code = "ADMIN", Name = "Administrador", CreatedAt = now, UpdatedAt = now, Version = 1 });
        var user = _h.Context.AppUsers.Single(u => u.UserId == AssetFlowsHarness.UserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("ClaveActual1");
        _h.Context.RefreshTokens.AddRange(
            new RefreshToken { UserId = user.UserId, TokenHash = AuthSessionIssuer.HashToken(RawCurrentToken), FamilyId = _currentFamily, ExpiresAt = now.AddDays(5), CreatedAt = now },
            new RefreshToken { UserId = user.UserId, TokenHash = AuthSessionIssuer.HashToken("otro-equipo"), FamilyId = _otherFamily, ExpiresAt = now.AddDays(5), CreatedAt = now });
        _h.Context.UserExternalLogins.Add(new UserExternalLogin { UserId = user.UserId, Provider = "google", Subject = "sub-1", Email = "admin@gmail.test", LinkedAt = now });
        _h.Context.SaveChanges();
    }

    public void Dispose() => _h.Dispose();

    private Task<ChangePasswordResponse> Change(string current, string next)
        => new ChangePasswordHandler(_h.Context, _user, new FakeCookies(), _audit)
            .Handle(new ChangePasswordCommand(current, next), default).AsTask();

    [Fact]
    public async Task Contrasena_actual_incorrecta_rechazada_sin_cambios()
    {
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Change("equivocada", "NuevaClave22"));
        Assert.True(BCrypt.Net.BCrypt.Verify("ClaveActual1", _h.Context.AppUsers.Single(u => u.UserId == 1).PasswordHash));
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Cambia_hash_cierra_otras_sesiones_conserva_la_actual_y_no_audita_el_hash()
    {
        var result = await Change("ClaveActual1", "NuevaClave22");

        Assert.Equal(1, result.OtherSessionsClosed);
        var user = _h.Context.AppUsers.Single(u => u.UserId == 1);
        Assert.True(BCrypt.Net.BCrypt.Verify("NuevaClave22", user.PasswordHash));
        Assert.Null(_h.Context.RefreshTokens.Single(t => t.FamilyId == _currentFamily).RevokedAt);
        Assert.NotNull(_h.Context.RefreshTokens.Single(t => t.FamilyId == _otherFamily).RevokedAt);

        var serialized = JsonSerializer.Serialize(Assert.Single(_audit.Entries).NewData);
        Assert.DoesNotContain(user.PasswordHash, serialized);
        Assert.Contains("PasswordChanged", serialized);
    }

    [Fact]
    public void Validador_rechaza_corta_o_igual_a_la_actual()
    {
        var v = new ChangePasswordValidator();
        Assert.False(v.Validate(new ChangePasswordCommand("ClaveActual1", "corta")).IsValid);
        Assert.False(v.Validate(new ChangePasswordCommand("ClaveActual1", "ClaveActual1")).IsValid);
        Assert.True(v.Validate(new ChangePasswordCommand("ClaveActual1", "NuevaClave22")).IsValid);
    }

    [Fact]
    public async Task Perfil_lista_vinculos_y_desvincular_borra_y_audita()
    {
        var profile = await new GetProfileHandler(_h.Context, _user).Handle(new GetProfileQuery(), default);
        Assert.Equal("google", Assert.Single(profile.ExternalLogins).Provider);

        await new UnlinkExternalLoginHandler(_h.Context, _user, _audit).Handle(new UnlinkExternalLoginCommand("Google"), default);

        Assert.Empty(_h.Context.UserExternalLogins);
        Assert.Equal(("user_external_logins", "DELETE"), (_audit.Entries.Single().TableName, _audit.Entries.Single().Action));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new UnlinkExternalLoginHandler(_h.Context, _user, _audit).Handle(new UnlinkExternalLoginCommand("google"), default).AsTask());
    }
}
