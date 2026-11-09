using System.Text;
using ClosedXML.Excel;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Pdf;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Reports.GetReport;
using TecnmInventario.Api.Features.Reports.Shared;
using SoftwareEntity = TecnmInventario.Api.Core.Entities.Software;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Los cinco reportes en Excel y PDF sobre PostgreSQL real (que EF traduzca las
/// proyecciones), con el contenido del Excel leído de vuelta y sin claves de licencia.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReportsIntegrationTests : PostgresTestBase
{
    public ReportsIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [DockerFact]
    public async Task Todos_los_reportes_se_generan_en_ambos_formatos_y_el_excel_trae_los_datos()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        h.AddAsset(AssetStatusCodes.DadoDeBaja);
        h.AddAssignment(asset.AssetId);
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Abierto);

        var now = DateTime.UtcNow;
        var software = new SoftwareEntity { Name = "Reporte prueba", CreatedAt = now, UpdatedAt = now, Version = 1 };
        h.Context.Softwares.Add(software);
        h.Context.SaveChanges();
        h.Context.SoftwareLicenses.Add(new SoftwareLicense
        {
            SoftwareId = software.SoftwareId, LicenseTypeId = h.Context.LicenseTypes.First().LicenseTypeId,
            LicenseKey = "CLAVE-SECRETA-REPORTE", QuantityPurchased = 5, CreatedAt = now, UpdatedAt = now, Version = 1
        });
        h.Context.MaintenanceRecords.Add(new MaintenanceRecord
        {
            AssetId = asset.AssetId, MaintenanceTypeId = h.Context.MaintenanceTypes.First().MaintenanceTypeId,
            PerformedAt = now, Description = "Limpieza", Cost = 150m, CreatedAt = now, UpdatedAt = now, Version = 1
        });
        h.Context.SaveChanges();

        var handler = new GetReportHandler(h.Context);
        foreach (var report in ReportCodes.All)
        {
            var pdf = await handler.Handle(new GetReportQuery(report, ReportCodes.Pdf, null, null, null, null), default);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf.Content, 0, 4));

            var xlsx = await handler.Handle(new GetReportQuery(report, ReportCodes.Xlsx, null, null, null, null), default);
            Assert.Equal(GeneratedFile.Xlsx, xlsx.ContentType);
            using var book = new XLWorkbook(new MemoryStream(xlsx.Content));
            var text = string.Join("|", book.Worksheet(1).CellsUsed().Select(c => c.GetString()));
            Assert.DoesNotContain("CLAVE-SECRETA", text);

            switch (report)
            {
                case ReportCodes.Assets:
                    Assert.Contains(asset.InventoryCode, text);
                    Assert.Contains("Técnico Integración", text); // resguardante vigente
                    break;
                case ReportCodes.OpenIncidents:
                    Assert.Contains("Falla de prueba", text);
                    break;
                case ReportCodes.Maintenance:
                    Assert.Contains("Limpieza", text);
                    break;
                case ReportCodes.Licenses:
                    Assert.Contains("Reporte prueba", text);
                    break;
            }
        }

        // Sin dados de baja por omisión; con el filtro de estado sí salen.
        var all = await handler.Handle(new GetReportQuery(ReportCodes.Assets, ReportCodes.Xlsx, null, null, null, null), default);
        var retired = await handler.Handle(new GetReportQuery(ReportCodes.Assets, ReportCodes.Xlsx, AssetStatusCodes.DadoDeBaja, null, null, null), default);
        Assert.Equal(1, DataRows(all));
        Assert.Equal(1, DataRows(retired));
    }

    [Fact]
    public void Validador_rechaza_reporte_formato_y_rango_invalidos()
    {
        var v = new GetReportValidator();
        Assert.False(v.Validate(new GetReportQuery("inventado", "xlsx", null, null, null, null)).IsValid);
        Assert.False(v.Validate(new GetReportQuery(ReportCodes.Assets, "docx", null, null, null, null)).IsValid);
        Assert.False(v.Validate(new GetReportQuery(ReportCodes.Maintenance, "pdf", null, null, new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1))).IsValid);
        Assert.True(v.Validate(new GetReportQuery(ReportCodes.Maintenance, "pdf", null, null, new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 1))).IsValid);
    }

    private static int DataRows(GeneratedFile file)
    {
        using var book = new XLWorkbook(new MemoryStream(file.Content));
        return book.Worksheet(1).Table("Datos").DataRange.RowCount();
    }
}
