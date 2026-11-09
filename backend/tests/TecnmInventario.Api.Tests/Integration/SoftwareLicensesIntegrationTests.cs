using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Software.Licenses.AssignLicense;
using TecnmInventario.Api.Features.Software.Licenses.EndLicenseAssignment;
using SoftwareEntity = TecnmInventario.Api.Core.Entities.Software;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Licencias sobre PostgreSQL real: el SELECT ... FOR UPDATE serializa dos
/// asignaciones simultáneas del último lugar, y los triggers suben la versión.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SoftwareLicensesIntegrationTests : PostgresTestBase
{
    public SoftwareLicensesIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    private static async Task<long> SeedLicenseAsync(TecnmInventarioDbContext context, int quantity)
    {
        var now = DateTime.UtcNow;
        var software = new SoftwareEntity { Name = $"Prueba {Guid.NewGuid():N}"[..20], CreatedAt = now, UpdatedAt = now, Version = 1 };
        context.Softwares.Add(software);
        await context.SaveChangesAsync();

        var license = new SoftwareLicense
        {
            SoftwareId = software.SoftwareId,
            LicenseTypeId = (await context.LicenseTypes.FirstAsync()).LicenseTypeId,
            LicenseKey = "SECRETA-0000-1111",
            QuantityPurchased = quantity,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        context.SoftwareLicenses.Add(license);
        await context.SaveChangesAsync();
        return license.LicenseId;
    }

    private static AssignLicenseHandler Handler(TecnmInventarioDbContext context)
    {
        var user = new FakeCurrentUser();
        return new AssignLicenseHandler(context, user, new AuditLogService(context, user));
    }

    [DockerFact]
    public async Task Dos_asignaciones_simultaneas_del_ultimo_lugar_solo_una_pasa()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var licenseId = await SeedLicenseAsync(h.Context, quantity: 1);
        var assetA = h.AddAsset(AssetStatusCodes.Disponible).AssetId;
        var assetB = h.AddAsset(AssetStatusCodes.Disponible).AssetId;

        await using var contextA = db.CreateContext();
        await using var contextB = db.CreateContext();
        await using var txA = await contextA.Database.BeginTransactionAsync();
        await using var txB = await contextB.Database.BeginTransactionAsync();

        // A toma el bloqueo y asigna, pero todavía no confirma.
        await Handler(contextA).Handle(new AssignLicenseCommand(licenseId, assetA), default);

        // B debe quedarse esperando el bloqueo de la fila de la licencia.
        var assignB = Handler(contextB).Handle(new AssignLicenseCommand(licenseId, assetB), default).AsTask();
        await Task.Delay(500);
        Assert.False(assignB.IsCompleted);

        await txA.CommitAsync();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => assignB);
        await txB.RollbackAsync();

        Assert.Equal(1, await db.CountAsync("software_license_assignments", $"license_id = {licenseId} and ended_at is null"));
    }

    [DockerFact]
    public async Task Asignar_y_liberar_auditan_sin_clave_y_el_trigger_sube_la_version()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var licenseId = await SeedLicenseAsync(h.Context, quantity: 1);
        var assetId = h.AddAsset(AssetStatusCodes.Asignado).AssetId;

        AssignLicenseResponse assigned;
        await using (var tx = await h.Context.Database.BeginTransactionAsync())
        {
            assigned = await Handler(h.Context).Handle(new AssignLicenseCommand(licenseId, assetId), default);
            await tx.CommitAsync();
        }

        var user = new FakeCurrentUser();
        var ended = await new EndLicenseAssignmentHandler(h.Context, user, new AuditLogService(h.Context, user))
            .Handle(new EndLicenseAssignmentCommand(assigned.LicenseAssignmentId, assigned.Version), default);

        Assert.True(ended.Version > assigned.Version);

        var audits = await h.Context.AuditLogs.AsNoTracking()
            .Where(a => a.TableName == "software_license_assignments" && a.RecordId == assigned.LicenseAssignmentId)
            .ToListAsync();
        Assert.Equal(new[] { "INSERT", "UPDATE" }, audits.OrderBy(a => a.AuditLogId).Select(a => a.Action));
        Assert.All(audits, a => Assert.DoesNotContain("SECRETA", (a.OldData ?? "") + a.NewData));
    }
}
