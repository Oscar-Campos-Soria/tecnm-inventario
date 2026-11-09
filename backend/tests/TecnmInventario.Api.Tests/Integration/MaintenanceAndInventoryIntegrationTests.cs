using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Features.Incidents.Shared;
using TecnmInventario.Api.Features.Inventory.CancelInventorySession;
using TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;
using TecnmInventario.Api.Features.Maintenance.CreateMaintenance;
using TecnmInventario.Api.Features.Maintenance.GetMaintenance;
using TecnmInventario.Api.Features.Maintenance.GetScheduledMaintenance;
using TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Correctivo que resuelve su incidente en la misma transacción (y si falla no
/// queda nada), mantenimientos programados y cancelar un conteo, sobre PostgreSQL real.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MaintenanceAndInventoryIntegrationTests : PostgresTestBase
{
    public MaintenanceAndInventoryIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    private static CreateMaintenanceHandler Create(TecnmInventarioDbContext context)
    {
        var user = new FakeCurrentUser();
        var audit = new AuditLogService(context, user);
        var resolver = new IncidentResolver(context, user, audit, new AssetStatusChanger(context, user, audit));
        return new CreateMaintenanceHandler(context, user, audit, new RecordingRolePermissionService(), resolver);
    }

    private static long TypeId(TecnmInventarioDbContext context, string code)
        => context.MaintenanceTypes.Single(t => t.Code == code).MaintenanceTypeId;

    [DockerFact]
    public async Task Correctivo_resuelve_el_incidente_y_libera_el_activo_en_una_transaccion()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.ConFalla);
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Abierto);
        var incidentId = h.Context.Incidents.Single(i => i.AssetId == asset.AssetId).IncidentId;

        await using (var tx = await h.Context.Database.BeginTransactionAsync())
        {
            var result = await Create(h.Context).Handle(new CreateMaintenanceCommand(
                asset.AssetId, TypeId(h.Context, MaintenanceTypeCodes.Correctivo), null, DateTime.UtcNow, null,
                "Se cambió la fuente de poder", null, 850m, incidentId, ResolveIncident: true), default);
            Assert.Equal(AssetStatusCodes.Disponible, result.AssetStatusAfter);
            await tx.CommitAsync();
        }

        Assert.Equal(AssetStatusCodes.Disponible, h.StatusOf(asset.AssetId).AssetStatus.Code);
        var incident = h.Context.Incidents.AsNoTracking().Include(i => i.IncidentStatus).Single(i => i.IncidentId == incidentId);
        Assert.Equal((IncidentStatusCodes.Resuelto, "Se cambió la fuente de poder"), (incident.IncidentStatus.Code, incident.Solution));
        Assert.Equal(1, await db.CountAsync("maintenance_records", $"incident_id = {incidentId}"));
    }

    [DockerFact]
    public async Task Si_resolver_falla_no_queda_el_correctivo()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Disponible);
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Resuelto);
        var incidentId = h.Context.Incidents.Single(i => i.AssetId == asset.AssetId).IncidentId;

        await using (var tx = await h.Context.Database.BeginTransactionAsync())
        {
            await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Create(h.Context).Handle(new CreateMaintenanceCommand(
                asset.AssetId, TypeId(h.Context, MaintenanceTypeCodes.Correctivo), null, DateTime.UtcNow, null,
                "Revisión", null, null, incidentId, ResolveIncident: true), default).AsTask());
            await tx.RollbackAsync();
        }

        Assert.Equal(0, await db.CountAsync("maintenance_records", $"asset_id = {asset.AssetId}"));
    }

    [DockerFact]
    public async Task Programado_sale_en_pendientes_no_en_historial_y_al_completarlo_se_invierte()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var user = new FakeCurrentUser();
        var audit = new AuditLogService(h.Context, user);

        var scheduled = await new ScheduleMaintenanceHandler(h.Context, user, audit, new RecordingRolePermissionService())
            .Handle(new ScheduleMaintenanceCommand(asset.AssetId, TypeId(h.Context, MaintenanceTypeCodes.Preventivo),
                DateTime.UtcNow.AddDays(10), "Limpieza interna", null), default);

        var pending = await new GetScheduledMaintenanceHandler(h.Context).Handle(new GetScheduledMaintenanceQuery(30), default);
        Assert.Contains(pending, m => m.MaintenanceRecordId == scheduled.MaintenanceRecordId && m.ScheduledAt != null);
        var history = await new GetMaintenanceHandler(h.Context).Handle(new GetMaintenanceQuery(1, 20, asset.AssetId, null), default);
        Assert.Empty(history.Items);

        var completed = await new CompleteMaintenanceHandler(h.Context, user, audit).Handle(new CompleteMaintenanceCommand(
            scheduled.MaintenanceRecordId, scheduled.Version, DateTime.UtcNow, null, "Pasta térmica", 120m, DateTime.UtcNow.AddMonths(6), null), default);
        Assert.True(completed.Version > scheduled.Version);

        Assert.DoesNotContain(await new GetScheduledMaintenanceHandler(h.Context).Handle(new GetScheduledMaintenanceQuery(30), default),
            m => m.MaintenanceRecordId == scheduled.MaintenanceRecordId);
        h.Context.ChangeTracker.Clear();
        Assert.Single((await new GetMaintenanceHandler(h.Context).Handle(new GetMaintenanceQuery(1, 20, asset.AssetId, null), default)).Items);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => new CompleteMaintenanceHandler(h.Context, user, audit).Handle(
            new CompleteMaintenanceCommand(scheduled.MaintenanceRecordId, completed.Version, DateTime.UtcNow, null, null, null, null, null), default).AsTask());
    }

    [DockerFact]
    public async Task Cancelar_conteo_abierto_guarda_motivo_y_libera_la_ubicacion()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var session = new InventorySession { Name = "Conteo por error", Status = InventorySessionStatusCodes.Abierta, LocationId = AssetFlowsHarness.LocationA, StartedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Version = 1 };
        h.Context.InventorySessions.Add(session);
        h.Context.SaveChanges();
        var user = new FakeCurrentUser();
        var handler = new CancelInventorySessionHandler(h.Context, user, new AuditLogService(h.Context, user));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            handler.Handle(new CancelInventorySessionCommand(session.InventorySessionId, 9, "Ubicación equivocada"), default).AsTask());

        var result = await handler.Handle(new CancelInventorySessionCommand(session.InventorySessionId, 1, "Ubicación equivocada"), default);

        Assert.Equal(InventorySessionStatusCodes.Cancelada, result.Status);
        Assert.Equal(1, await db.CountAsync("audit_log", $"table_name = 'inventory_sessions' and new_data ->> 'Reason' = 'Ubicación equivocada'"));
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(new CancelInventorySessionCommand(session.InventorySessionId, result.Version, "Otra vez"), default).AsTask());
    }
}
