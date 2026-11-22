using System.Text.Json.Nodes;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.GetAssets;
using TecnmInventario.Api.Features.Assistant.Shared;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Herramientas del asistente con Mediator real (handlers generados, pipeline de negocio) sobre PostgreSQL:
/// los conteos que el asistente le da al modelo deben salir iguales a los registros de la query.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AssistantToolsIntegrationTests : PostgresTestBase
{
    public AssistantToolsIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    private sealed class AllowAll : IRolePermissionService
    {
        public Task<bool> HasPermissionAsync(string roleCode, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public void InvalidateCache(string roleCode) { }
    }

    [DockerFact]
    public async Task Conteo_por_estado_de_buscar_activos_coincide_con_los_registros()
    {
        await using var database = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(database);
        foreach (var status in new[] { AssetStatusCodes.Disponible, AssetStatusCodes.Disponible, AssetStatusCodes.Asignado,
                     AssetStatusCodes.ConFalla, AssetStatusCodes.ConFalla, AssetStatusCodes.ConFalla, AssetStatusCodes.DadoDeBaja })
            h.AddAsset(status);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TecnmInventarioDbContext>(o => o.UseNpgsql(database.ConnectionString));
        services.AddMediator(o => o.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddBusinessPipeline();
        services.AddScoped<ICurrentUserService, FakeCurrentUser>();
        services.AddSingleton<IRolePermissionService, AllowAll>();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<TecnmInventarioDbContext>();
        var context = new ToolContext(mediator, db, await AiCatalog.LoadAsync(db, default), DateTime.UtcNow,
            new HashSet<string> { "assets.view" });
        var tool = new SearchAssetsTool();

        var all = (await tool.ExecuteAsync(new ToolArgs([]), context, default)).Data;
        var faulty = (await tool.ExecuteAsync(new ToolArgs(new JsonObject { ["estado"] = "CON_FALLA" }), context, default)).Data;

        // Verdad: los registros de la misma query, estado por estado (los dados de baja no se listan).
        foreach (var status in new[] { AssetStatusCodes.Disponible, AssetStatusCodes.Asignado, AssetStatusCodes.ConFalla })
        {
            var rows = await mediator.Send(new GetAssetsQuery(1, 100, null, status, null, null, false), default);
            Assert.Equal(rows.Items.Count, (int)all["por_estado"]![status]!);
            Assert.Equal(rows.Items.Count, (int)faulty["por_estado"]![status]!);
        }
        Assert.Equal(6, (int)all["total"]!);
        Assert.Null(all["por_estado"]![AssetStatusCodes.DadoDeBaja]);
        Assert.Equal(3, (int)faulty["total"]!);
        Assert.Equal(6, (int)faulty["total_sin_filtro_de_estado"]!); // "¿cuántas hay y cuáles fallan?" con una consulta
        Assert.Null(all["total_sin_filtro_de_estado"]);
    }
}
