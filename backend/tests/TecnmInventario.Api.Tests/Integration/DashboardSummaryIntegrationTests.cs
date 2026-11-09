using Microsoft.Extensions.Caching.Memory;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Dashboard.GetSummary;
using SoftwareEntity = TecnmInventario.Api.Core.Entities.Software;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Agregados del panel sobre PostgreSQL real (usa v_asset_summary, que
/// InMemory no soporta): los dados de baja no cuentan como en servicio,
/// licencias vencidas y por vencer, y la caché de 60 s.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DashboardSummaryIntegrationTests : PostgresTestBase
{
    public DashboardSummaryIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Cuenta_activos_en_servicio_licencias_y_respeta_la_cache()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        h.AddAsset(AssetStatusCodes.Disponible);
        h.AddAsset(AssetStatusCodes.Asignado);
        h.AddAsset(AssetStatusCodes.DadoDeBaja);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var software = new SoftwareEntity { Name = "Panel prueba", CreatedAt = now, UpdatedAt = now, Version = 1 };
        h.Context.Softwares.Add(software);
        h.Context.SaveChanges();
        var licenseTypeId = h.Context.LicenseTypes.First().LicenseTypeId;
        foreach (var until in new DateOnly?[] { today.AddDays(-1), today.AddDays(10), today.AddDays(90), null })
        {
            h.Context.SoftwareLicenses.Add(new SoftwareLicense
            {
                SoftwareId = software.SoftwareId, LicenseTypeId = licenseTypeId, ValidUntil = until,
                CreatedAt = now, UpdatedAt = now, Version = 1
            });
        }
        h.Context.SaveChanges();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetDashboardSummaryHandler(h.Context, cache);

        var summary = await handler.Handle(new GetDashboardSummaryQuery(), default);

        Assert.Equal(2, summary.TotalAssets);
        Assert.Equal(1, summary.AssetsByStatus.Single(s => s.Code == AssetStatusCodes.DadoDeBaja).Count);
        Assert.Equal(2, summary.AssetsByType.Sum(t => t.Count));
        Assert.Equal(2, summary.AssetsByLocation.Sum(l => l.Count));
        Assert.Equal((1, 1), (summary.LicensesExpired, summary.LicensesExpiringSoon));
        Assert.All(summary.OpenIncidentsByPriority, p => Assert.Equal(0, p.Count));
        Assert.NotEmpty(summary.OpenIncidentsByPriority);

        // Dentro de los 60 s se devuelve lo cacheado aunque cambien los datos.
        h.AddAsset(AssetStatusCodes.Disponible);
        Assert.Same(summary, await handler.Handle(new GetDashboardSummaryQuery(), default));
    }
}
