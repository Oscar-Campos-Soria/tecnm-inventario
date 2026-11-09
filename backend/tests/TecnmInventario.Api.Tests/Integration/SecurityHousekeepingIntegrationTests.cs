using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Auth.Shared;
using TecnmInventario.Api.Features.History.GetAssetTimeline;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Limpieza de refresh tokens (familias completas, respetando replaced_by_id) y
/// línea de tiempo del activo con sus compras, sobre PostgreSQL real.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SecurityHousekeepingIntegrationTests : PostgresTestBase
{
    public SecurityHousekeepingIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Limpieza_borra_solo_familias_muertas_completas()
    {
        await using var db = await CreateDatabaseAsync();
        await using var context = db.CreateContext();
        var userId = await context.AppUsers.Select(u => u.UserId).FirstAsync();
        var now = DateTime.UtcNow;
        var dead = Guid.NewGuid();
        var alive = Guid.NewGuid();

        // Familia muerta: cadena rotada (el viejo apunta al nuevo) y todo vencido hace 40 días.
        var newer = Token(dead, now.AddDays(-40), revoked: false);
        context.RefreshTokens.Add(newer);
        await context.SaveChangesAsync();
        var older = Token(dead, now.AddDays(-45), revoked: true);
        older.ReplacedById = newer.RefreshTokenId;
        // Familia viva: un token revocado viejo y uno vigente; se conservan ambos.
        context.RefreshTokens.AddRange(older, Token(alive, now.AddDays(-45), revoked: true), Token(alive, now.AddDays(5), revoked: false));
        await context.SaveChangesAsync();

        var deleted = await RefreshTokenCleanup.DeleteDeadFamiliesAsync(context, now, default);

        Assert.Equal(2, deleted);
        Assert.Equal(0, await db.CountAsync("refresh_tokens", $"family_id = '{dead}'"));
        Assert.Equal(2, await db.CountAsync("refresh_tokens", $"family_id = '{alive}'"));

        RefreshToken Token(Guid family, DateTime expires, bool revoked) => new()
        {
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            FamilyId = family,
            ExpiresAt = expires,
            CreatedAt = expires.AddDays(-7),
            RevokedAt = revoked ? expires.AddDays(-1) : null
        };
    }

    [DockerFact]
    public async Task Linea_de_tiempo_incluye_la_compra_del_activo()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Disponible);
        var other = h.AddAsset(AssetStatusCodes.Disponible);

        // Igual que CreateAcquisition: el snapshot lista los activos, sin AssetId en la raíz.
        await db.ExecuteAsync($$"""
            INSERT INTO inventario.acquisitions (acquisition_id, currency_code) OVERRIDING SYSTEM VALUE VALUES (900, 'MXN');
            INSERT INTO inventario.acquisition_assets (acquisition_id, asset_id) VALUES (900, {{asset.AssetId}});
            INSERT INTO inventario.audit_log (table_name, record_id, action, new_data)
            VALUES ('acquisitions', 900, 'INSERT', '{"Assets":[{"AssetId":{{asset.AssetId}}}]}');
            """);

        var timeline = await new GetAssetTimelineHandler(h.Context).Handle(new GetAssetTimelineQuery(asset.AssetId), default);
        var otherTimeline = await new GetAssetTimelineHandler(h.Context).Handle(new GetAssetTimelineQuery(other.AssetId), default);

        Assert.Contains(timeline, e => e.TableName == "acquisitions" && e.RecordId == 900);
        Assert.DoesNotContain(otherTimeline, e => e.TableName == "acquisitions");
    }
}
