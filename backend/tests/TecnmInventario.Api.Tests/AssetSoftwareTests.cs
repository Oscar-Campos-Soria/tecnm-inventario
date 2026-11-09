using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Software.Installations.GetAssetSoftware;
using TecnmInventario.Api.Features.Software.Installations.InstallSoftware;
using TecnmInventario.Api.Features.Software.Installations.UninstallSoftware;
using SoftwareEntity = TecnmInventario.Api.Core.Entities.Software;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Software instalado por equipo: una versión nueva reemplaza a la vigente
/// (queda historial), no se instala en activos dados de baja y desinstalar
/// avisa si el equipo sigue ocupando una licencia. Proveedor en memoria.
/// </summary>
public class AssetSoftwareTests : IDisposable
{
    private readonly AssetFlowsHarness _h = AssetFlowsHarness.CreateInMemory();
    private readonly RecordingAuditLogService _audit = new();
    private readonly FakeCurrentUser _user = new();

    public AssetSoftwareTests()
    {
        var now = DateTime.UtcNow;
        _h.Context.Softwares.Add(new SoftwareEntity { SoftwareId = 1, Name = "Chrome", Vendor = "Google", CreatedAt = now, UpdatedAt = now, Version = 1 });
        _h.Context.SaveChanges();
    }

    public void Dispose() => _h.Dispose();

    private Task<InstallSoftwareResponse> Install(long assetId, string? version)
        => new InstallSoftwareHandler(_h.Context, _user, _audit)
            .Handle(new InstallSoftwareCommand(assetId, 1, version, null), default).AsTask();

    private Task<UninstallSoftwareResponse> Uninstall(long assetId, long id, int version)
        => new UninstallSoftwareHandler(_h.Context, _user, _audit)
            .Handle(new UninstallSoftwareCommand(assetId, id, version), default).AsTask();

    private Task<IReadOnlyList<GetAssetSoftwareResponse>> List(long assetId, bool history)
        => new GetAssetSoftwareHandler(_h.Context).Handle(new GetAssetSoftwareQuery(assetId, history), default).AsTask();

    [Fact]
    public async Task Nueva_version_reemplaza_a_la_vigente_y_queda_historial()
    {
        var asset = _h.AddAsset(AssetStatusCodes.Asignado);

        var first = await Install(asset.AssetId, "120");
        var second = await Install(asset.AssetId, " 121 ");

        Assert.Equal(first.AssetSoftwareId, second.ReplacedAssetSoftwareId);
        Assert.Equal("121", Assert.Single(await List(asset.AssetId, history: false)).VersionInstalled);
        Assert.Equal(2, (await List(asset.AssetId, history: true)).Count);
        Assert.Equal(
            new[] { ("INSERT", first.AssetSoftwareId), ("UPDATE", first.AssetSoftwareId), ("INSERT", second.AssetSoftwareId) },
            _audit.Entries.Select(e => (e.Action, e.RecordId)));
    }

    [Fact]
    public async Task Misma_version_o_activo_dado_de_baja_rechazado()
    {
        var asset = _h.AddAsset(AssetStatusCodes.Disponible);
        await Install(asset.AssetId, "120");
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Install(asset.AssetId, "120"));

        var retired = _h.AddAsset(AssetStatusCodes.DadoDeBaja);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Install(retired.AssetId, "120"));
    }

    [Fact]
    public async Task Desinstalar_avisa_licencia_vigente_segunda_vez_rechazada_y_version_vieja_409()
    {
        var asset = _h.AddAsset(AssetStatusCodes.Disponible);
        var installed = await Install(asset.AssetId, null);

        var now = DateTime.UtcNow;
        _h.Context.AddRange(
            new LicenseType { LicenseTypeId = 1, Code = "OEM", Name = "OEM", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new SoftwareLicense { LicenseId = 1, SoftwareId = 1, LicenseTypeId = 1, CreatedAt = now, UpdatedAt = now, Version = 1 },
            new SoftwareLicenseAssignment { LicenseId = 1, AssetId = asset.AssetId, AssignedAt = now, UpdatedAt = now, Version = 1 });
        _h.Context.SaveChanges();

        Assert.True(Assert.Single(await List(asset.AssetId, false)).HasActiveLicense);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Uninstall(asset.AssetId, installed.AssetSoftwareId, 5));

        var result = await Uninstall(asset.AssetId, installed.AssetSoftwareId, installed.Version);
        Assert.True(result.HasActiveLicense);
        Assert.Empty(await List(asset.AssetId, false));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Uninstall(asset.AssetId, installed.AssetSoftwareId, installed.Version));
    }

    [Fact]
    public async Task Instalacion_de_otro_equipo_es_404()
    {
        var a = _h.AddAsset(AssetStatusCodes.Disponible);
        var b = _h.AddAsset(AssetStatusCodes.Disponible);
        var installed = await Install(a.AssetId, "1");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Uninstall(b.AssetId, installed.AssetSoftwareId, 1));
    }
}
