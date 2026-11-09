using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Users.ChangeUserStatus;
using TecnmInventario.Api.Features.Users.ResetUserPassword;
using TecnmInventario.Api.Features.Users.UpdateUser;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Administración de usuarios: protecciones anti-bloqueo (siempre un ADMIN
/// activo, nadie se quita su propio acceso), cierre de sesiones, regla 15
/// (sin cambios no audita; versión vieja 409) y regla 16 (sin secretos).
/// FakeCurrentUser es el usuario 1 (ADMIN).
/// </summary>
public class UserAdministrationTests
{
    private const long Me = 1;
    private const long OtherAdmin = 2;
    private const long Tecnico = 3;

    private static (TecnmInventarioDbContext Ctx, RecordingAuditLogService Audit) Setup(bool withSecondAdmin)
    {
        var ctx = TestDb.Create();
        var now = DateTime.UtcNow;
        ctx.AddRange(
            new Role { RoleId = 1, Code = "ADMIN", Name = "Administrador", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new Role { RoleId = 2, Code = "TECNICO", Name = "Técnico", CreatedAt = now, UpdatedAt = now, Version = 1 },
            User(Me, "yo", 1),
            User(Tecnico, "tec", 2));
        if (withSecondAdmin) ctx.Add(User(OtherAdmin, "otro", 1));
        ctx.Add(new RefreshToken
        {
            RefreshTokenId = 1, UserId = Tecnico, TokenHash = new string('a', 64), FamilyId = Guid.NewGuid(),
            ExpiresAt = now.AddDays(5), CreatedAt = now
        });
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return (ctx, new RecordingAuditLogService());

        AppUser User(long id, string username, long roleId) => new()
        {
            UserId = id, Username = username, Email = $"{username}@test.local", PasswordHash = "x",
            FirstName = username, LastName = "Test", RoleId = roleId, Status = "activo",
            CreatedAt = now, UpdatedAt = now, Version = 1
        };
    }

    private static UpdateUserHandler Update(TecnmInventarioDbContext c, RecordingAuditLogService a) => new(c, new FakeCurrentUser(), a);
    private static ChangeUserStatusHandler Status(TecnmInventarioDbContext c, RecordingAuditLogService a) => new(c, new FakeCurrentUser(), a);

    [Fact]
    public async Task CannotDeactivateOwnAccount()
    {
        var (ctx, audit) = Setup(withSecondAdmin: true);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Status(ctx, audit).Handle(new ChangeUserStatusCommand(Me, 1, "inactivo"), default).AsTask());
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task CannotRemoveOwnAdminRole()
    {
        var (ctx, audit) = Setup(withSecondAdmin: true);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Update(ctx, audit).Handle(new UpdateUserCommand(Me, 1, "yo@test.local", "yo", "Test", "TECNICO"), default).AsTask());
    }

    [Fact]
    public async Task LastActiveAdmin_CannotLoseAdmin()
    {
        // Con Me inactivo, OtherAdmin es el único admin activo: no se puede
        // suspender ni quitarle el rol.
        var (ctx, audit) = Setup(withSecondAdmin: true);
        var me = ctx.AppUsers.Single(u => u.UserId == Me);
        me.Status = "inactivo";
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Status(ctx, audit).Handle(new ChangeUserStatusCommand(OtherAdmin, 1, "suspendido"), default).AsTask());
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Update(ctx, audit).Handle(new UpdateUserCommand(OtherAdmin, 1, "otro@test.local", "otro", "Test", "TECNICO"), default).AsTask());
    }

    [Fact]
    public async Task Deactivate_RevokesSessions_AndAudits()
    {
        var (ctx, audit) = Setup(withSecondAdmin: false);
        var result = await Status(ctx, audit).Handle(new ChangeUserStatusCommand(Tecnico, 1, "inactivo"), default);

        Assert.Equal(1, result.SessionsRevoked);
        Assert.NotNull(ctx.RefreshTokens.AsNoTracking().Single().RevokedAt);
        Assert.Equal("inactivo", ctx.AppUsers.AsNoTracking().Single(u => u.UserId == Tecnico).Status);
        Assert.Single(audit.Entries, e => e.TableName == "app_users" && e.Action == "UPDATE");
    }

    [Fact]
    public async Task UpdateWithoutChanges_DoesNotAudit_StaleVersionConflicts()
    {
        var (ctx, audit) = Setup(withSecondAdmin: false);
        await Update(ctx, audit).Handle(new UpdateUserCommand(Tecnico, 1, "tec@test.local", "tec", "Test", "TECNICO"), default);
        Assert.Empty(audit.Entries);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => Update(ctx, audit).Handle(new UpdateUserCommand(Tecnico, 7, "tec@test.local", "tec", "Test", "TECNICO"), default).AsTask());
    }

    [Fact]
    public async Task ResetPassword_HashesAndNeverAuditsSecret()
    {
        var (ctx, audit) = Setup(withSecondAdmin: false);
        await new ResetUserPasswordHandler(ctx, new FakeCurrentUser(), audit)
            .Handle(new ResetUserPasswordCommand(Tecnico, 1, "NuevaClave2026"), default);

        var hash = ctx.AppUsers.AsNoTracking().Single(u => u.UserId == Tecnico).PasswordHash;
        Assert.True(BCrypt.Net.BCrypt.Verify("NuevaClave2026", hash));
        var json = JsonSerializer.Serialize(audit.Entries.Single().NewData);
        Assert.DoesNotContain("NuevaClave2026", json);
        Assert.DoesNotContain(hash, json);
        Assert.NotNull(ctx.RefreshTokens.AsNoTracking().Single().RevokedAt);
    }
}
