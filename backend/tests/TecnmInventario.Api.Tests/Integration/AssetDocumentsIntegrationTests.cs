using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Documents;
using TecnmInventario.Api.Features.Documents.GetAssetDocuments;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Documentos de la ficha del activo: junta los archivos de sus incidentes,
/// mantenimientos y compras, omite borrados, los de otro activo y los dueños
/// que el autorizador niega. En InMemory (rápido) y en PostgreSQL real
/// (traducción de la consulta y trigger que valida al dueño).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AssetDocumentsIntegrationTests : PostgresTestBase
{
    public AssetDocumentsIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public Task InMemory_junta_documentos_del_activo_y_filtra_por_acceso()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        return Scenario(h);
    }

    [DockerFact]
    public async Task Postgres_junta_documentos_del_activo_y_filtra_por_acceso()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        await Scenario(h);
    }

    private static async Task Scenario(AssetFlowsHarness h)
    {
        var asset = h.AddAsset(AssetStatusCodes.Disponible);
        var other = h.AddAsset(AssetStatusCodes.Disponible);
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Abierto);
        h.AddIncident(other.AssetId, IncidentStatusCodes.Abierto);
        var incidentId = h.Context.Incidents.Single(i => i.AssetId == asset.AssetId).IncidentId;
        var otherIncidentId = h.Context.Incidents.Single(i => i.AssetId == other.AssetId).IncidentId;

        var now = DateTime.UtcNow;
        if (!h.Context.MaintenanceTypes.Any()) // InMemory no siembra este catálogo
        {
            h.Context.MaintenanceTypes.Add(new MaintenanceType { Code = MaintenanceTypeCodes.Preventivo, Name = "Preventivo" });
            h.Context.SaveChanges();
        }
        var maintenance = new MaintenanceRecord
        {
            AssetId = asset.AssetId, MaintenanceTypeId = h.Context.MaintenanceTypes.First().MaintenanceTypeId,
            PerformedAt = now, Description = "Limpieza", CreatedAt = now, UpdatedAt = now, Version = 1
        };
        var acquisition = new Acquisition { PurchaseOrder = "OC-77", CurrencyCode = "MXN", CreatedAt = now, UpdatedAt = now, Version = 1 };
        h.Context.MaintenanceRecords.Add(maintenance);
        h.Context.Acquisitions.Add(acquisition);
        h.Context.SaveChanges();
        h.Context.AcquisitionAssets.Add(new AcquisitionAsset { AcquisitionId = acquisition.AcquisitionId, AssetId = asset.AssetId });

        Document Doc(string type, long id, string name, bool deleted = false) => new()
        {
            OwnerType = type, OwnerId = id, FileName = name, StorageKey = $"{Guid.NewGuid():N}.pdf",
            MimeType = "application/pdf", SizeBytes = 10, CreatedAt = now, UpdatedAt = now,
            IsDeleted = deleted, DeletedAt = deleted ? now : null, Version = 1
        };
        h.Context.Documents.AddRange(
            Doc(DocumentOwnerTypes.Incident, incidentId, "foto.pdf"),
            Doc(DocumentOwnerTypes.Incident, incidentId, "borrado.pdf", deleted: true),
            Doc(DocumentOwnerTypes.Incident, otherIncidentId, "ajeno.pdf"),
            Doc(DocumentOwnerTypes.Maintenance, maintenance.MaintenanceRecordId, "servicio.pdf"),
            Doc(DocumentOwnerTypes.Acquisition, acquisition.AcquisitionId, "factura.pdf"));
        h.Context.SaveChanges();

        var all = await new GetAssetDocumentsHandler(h.Context, new DenyingAuthorizer())
            .Handle(new GetAssetDocumentsQuery(asset.AssetId), default);
        Assert.Equal(new[] { "factura.pdf", "foto.pdf", "servicio.pdf" }, all.Select(d => d.FileName).Order());
        Assert.Equal("Compra OC OC-77", all.Single(d => d.FileName == "factura.pdf").OwnerLabel);
        Assert.StartsWith($"Incidente #{incidentId}:", all.Single(d => d.FileName == "foto.pdf").OwnerLabel);

        // Sin acceso a compras (p. ej. TECNICO sin procurement.manage): la factura no sale.
        var limited = await new GetAssetDocumentsHandler(h.Context, new DenyingAuthorizer(DocumentOwnerTypes.Acquisition))
            .Handle(new GetAssetDocumentsQuery(asset.AssetId), default);
        Assert.DoesNotContain(limited, d => d.OwnerType == DocumentOwnerTypes.Acquisition);
        Assert.Equal(2, limited.Count);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new GetAssetDocumentsHandler(h.Context, new DenyingAuthorizer()).Handle(new GetAssetDocumentsQuery(999_999), default).AsTask());
    }

    private sealed class DenyingAuthorizer(params string[] denied) : IDocumentAccessAuthorizer
    {
        public Task EnsureAuthorizedAsync(string ownerType, long ownerId, DocumentOperation operation,
            long? documentCreatedBy, CancellationToken cancellationToken)
            => denied.Contains(ownerType) ? throw new ForbiddenAccessException("sin acceso") : Task.CompletedTask;
    }
}
