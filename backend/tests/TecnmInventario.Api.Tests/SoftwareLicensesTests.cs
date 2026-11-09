using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Software.Licenses.AssignLicense;
using TecnmInventario.Api.Features.Software.Licenses.DeleteLicense;
using TecnmInventario.Api.Features.Software.Licenses.EndLicenseAssignment;
using TecnmInventario.Api.Features.Software.Licenses.GetLicenseById;
using TecnmInventario.Api.Features.Software.Licenses.GetLicenses;
using TecnmInventario.Api.Features.Software.Licenses.UpdateLicense;
using TecnmInventario.Api.Features.Software.Programs.CreateSoftware;
using TecnmInventario.Api.Features.Software.Programs.DeleteSoftware;
using SoftwareEntity = TecnmInventario.Api.Core.Entities.Software;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Reglas de licencias: cupo (null = sin límite, 0 = sin cupo), vencimiento,
/// activos dados de baja, duplicados, liberación y que la clave nunca salga
/// completa ni llegue a audit_log. Proveedor en memoria (sin trigger de versión
/// ni bloqueo de fila; el bloqueo se prueba en Integration/SoftwareLicensesIntegrationTests).
/// </summary>
public class SoftwareLicensesTests : IDisposable
{
    private const string Key = "ABCDE-FGHIJ-KLMNO-12345";

    private readonly AssetFlowsHarness _h = AssetFlowsHarness.CreateInMemory();
    private readonly RecordingAuditLogService _audit = new();
    private readonly FakeCurrentUser _user = new();

    public SoftwareLicensesTests()
    {
        var now = DateTime.UtcNow;
        _h.Context.AddRange(
            new LicenseType { LicenseTypeId = 1, Code = "VOLUMEN", Name = "Volumen", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new SoftwareEntity { SoftwareId = 1, Name = "Office", Vendor = "Microsoft", CreatedAt = now, UpdatedAt = now, Version = 1 });
        _h.Context.SaveChanges();
    }

    public void Dispose() => _h.Dispose();

    private SoftwareLicense AddLicense(int? quantity, DateOnly? validUntil = null, string? key = Key)
    {
        var now = DateTime.UtcNow;
        var license = new SoftwareLicense
        {
            SoftwareId = 1, LicenseTypeId = 1, LicenseKey = key, QuantityPurchased = quantity,
            ValidUntil = validUntil, CreatedAt = now, UpdatedAt = now, Version = 1
        };
        _h.Context.SoftwareLicenses.Add(license);
        _h.Context.SaveChanges();
        return license;
    }

    private Task<AssignLicenseResponse> Assign(long licenseId, long assetId)
        => new AssignLicenseHandler(_h.Context, _user, _audit)
            .Handle(new AssignLicenseCommand(licenseId, assetId), default).AsTask();

    private Task<EndLicenseAssignmentResponse> End(long assignmentId, int version)
        => new EndLicenseAssignmentHandler(_h.Context, _user, _audit)
            .Handle(new EndLicenseAssignmentCommand(assignmentId, version), default).AsTask();

    private Task<UpdateLicenseResponse> Update(SoftwareLicense l, int? quantity, string? key, int? version = null)
        => new UpdateLicenseHandler(_h.Context, _user, _audit)
            .Handle(new UpdateLicenseCommand(
                l.LicenseId, version ?? l.Version, l.LicenseTypeId, key, quantity,
                l.ValidFrom, l.ValidUntil, l.SupplierId, l.Notes), default).AsTask();

    private SoftDeleteAuditor Deleter()
        => new(_h.Context, _audit, _user, new FakeTransactionScopeFactory(), new RecordingLogger<SoftDeleteAuditor>());

    [Fact]
    public async Task Assign_ConCupo_InsertaYAuditaSinClave()
    {
        var license = AddLicense(quantity: 2);
        var asset = _h.AddAsset(AssetStatusCodes.Asignado);

        var result = await Assign(license.LicenseId, asset.AssetId);

        Assert.Equal(1, result.InUse);
        Assert.Null(_h.Context.SoftwareLicenseAssignments.Single().EndedAt);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(("software_license_assignments", "INSERT"), (entry.TableName, entry.Action));
    }

    [Fact]
    public async Task Assign_SinCupo_Rechazado()
    {
        var license = AddLicense(quantity: 1);
        await Assign(license.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Assign(license.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId));
        Assert.Contains("1 de 1", ex.Message);
    }

    [Fact]
    public async Task Assign_CantidadNull_EsSinLimite_YCeroEsSinCupo()
    {
        var unlimited = AddLicense(quantity: null);
        for (var i = 0; i < 3; i++)
        {
            await Assign(unlimited.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId);
        }

        var none = AddLicense(quantity: 0);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Assign(none.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId));
    }

    [Fact]
    public async Task Assign_Vencida_BajaODuplicada_Rechazado()
    {
        var expired = AddLicense(quantity: 5, validUntil: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Assign(expired.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId));

        var license = AddLicense(quantity: 5);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Assign(license.LicenseId, _h.AddAsset(AssetStatusCodes.DadoDeBaja).AssetId));

        var asset = _h.AddAsset(AssetStatusCodes.Disponible);
        await Assign(license.LicenseId, asset.AssetId);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Assign(license.LicenseId, asset.AssetId));
    }

    [Fact]
    public async Task End_LiberaCupo_SegundaVezRechazada_VersionViejaEs409()
    {
        var license = AddLicense(quantity: 1);
        var assigned = await Assign(license.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => End(assigned.LicenseAssignmentId, 99));

        await End(assigned.LicenseAssignmentId, assigned.Version);
        Assert.NotNull(_h.Context.SoftwareLicenseAssignments.Single().EndedAt);
        Assert.Contains(_audit.Entries, e => e.TableName == "software_license_assignments" && e.Action == "UPDATE");

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => End(assigned.LicenseAssignmentId, assigned.Version));

        // El lugar liberado vuelve a estar disponible.
        await Assign(license.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId);
    }

    [Fact]
    public async Task Update_NoBajaCantidadPorDebajoDeLoAsignado()
    {
        var license = AddLicense(quantity: 3);
        await Assign(license.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId);
        await Assign(license.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Update(license, quantity: 1, key: null));
        await Update(license, quantity: 2, key: null);
        Assert.Equal(2, _h.Context.SoftwareLicenses.Single().QuantityPurchased);
    }

    [Fact]
    public async Task Update_SinCambiosNoAudita_VersionViejaEs409_ClaveNullConservaYVaciaQuita()
    {
        var license = AddLicense(quantity: 3);

        await Update(license, quantity: 3, key: null);
        Assert.Empty(_audit.Entries);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Update(license, 3, null, version: 7));

        await Update(license, quantity: 4, key: null);
        Assert.Equal(Key, _h.Context.SoftwareLicenses.Single().LicenseKey);

        await Update(license, quantity: 4, key: "");
        Assert.Null(_h.Context.SoftwareLicenses.Single().LicenseKey);
    }

    [Fact]
    public async Task Clave_SoloEnmascarada_YNuncaEnAuditLog()
    {
        var license = AddLicense(quantity: 2);
        await Assign(license.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId);
        await Update(license, quantity: 3, key: "NUEVA-CLAVE-9999");

        var detail = await new GetLicenseByIdHandler(_h.Context).Handle(new GetLicenseByIdQuery(license.LicenseId), default);
        Assert.Equal("••••9999", detail.MaskedLicenseKey);
        Assert.Equal((1, 2), (detail.InUse, detail.Available));

        var serialized = JsonSerializer.Serialize(_audit.Entries.Select(e => new { e.OldData, e.NewData }));
        Assert.DoesNotContain(Key, serialized);
        Assert.DoesNotContain("NUEVA-CLAVE", serialized);
        Assert.Contains("HasLicenseKey", serialized);
    }

    [Fact]
    public async Task GetLicenses_PorVencer_FiltraYCalculaDisponibles()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        AddLicense(quantity: 10, validUntil: today.AddDays(10));
        AddLicense(quantity: 10, validUntil: today.AddDays(90));
        AddLicense(quantity: null);

        var page = await new GetLicensesHandler(_h.Context)
            .Handle(new GetLicensesQuery(1, 20, null, ExpiringInDays: 30, null), default);

        var item = Assert.Single(page.Items);
        Assert.Equal((10, 10), (item.QuantityPurchased!.Value, item.Available!.Value));
    }

    [Fact]
    public async Task DeleteLicense_ConAsignacionVigente_Rechazado()
    {
        var license = AddLicense(quantity: 2);
        await Assign(license.LicenseId, _h.AddAsset(AssetStatusCodes.Disponible).AssetId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            new DeleteLicenseHandler(_h.Context, Deleter())
                .Handle(new DeleteLicenseCommand(license.LicenseId, license.Version), default).AsTask());
    }

    [Fact]
    public async Task DeleteSoftware_ConLicencias_Rechazado_SinLicenciasBorraLogico()
    {
        AddLicense(quantity: 1);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            new DeleteSoftwareHandler(_h.Context, Deleter())
                .Handle(new DeleteSoftwareCommand(1, 1), default).AsTask());

        var now = DateTime.UtcNow;
        _h.Context.Softwares.Add(new SoftwareEntity { SoftwareId = 2, Name = "7-Zip", CreatedAt = now, UpdatedAt = now, Version = 1 });
        _h.Context.SaveChanges();

        await new DeleteSoftwareHandler(_h.Context, Deleter()).Handle(new DeleteSoftwareCommand(2, 1), default);
        Assert.True(_h.Context.Softwares.IgnoreQueryFilters().Single(s => s.SoftwareId == 2).IsDeleted);
        Assert.Contains(_audit.Entries, e => e.TableName == "software" && e.Action == "DELETE");
    }

    [Fact]
    public async Task CreateSoftware_NombreYFabricanteDuplicado_Invalido_IncluyeFabricanteVacio()
    {
        var validator = new CreateSoftwareValidator(_h.Context);

        Assert.False((await validator.ValidateAsync(new CreateSoftwareCommand(" Office ", "Microsoft", null))).IsValid);
        Assert.True((await validator.ValidateAsync(new CreateSoftwareCommand("Office", "LibreOffice", null))).IsValid);

        await new CreateSoftwareHandler(_h.Context, _user, _audit)
            .Handle(new CreateSoftwareCommand("Notepad++", "  ", null), default);
        Assert.False((await validator.ValidateAsync(new CreateSoftwareCommand("Notepad++", null, null))).IsValid);
    }
}
