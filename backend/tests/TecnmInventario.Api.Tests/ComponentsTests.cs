using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Components;
using TecnmInventario.Api.Features.Components.AddComponent;
using TecnmInventario.Api.Features.Components.DeleteComponent;
using TecnmInventario.Api.Features.Components.GetAssetComponents;
using TecnmInventario.Api.Features.Components.RemoveComponent;
using TecnmInventario.Api.Features.Components.ReplaceComponent;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Componentes internos: reemplazar cierra el actual y abre uno del mismo
/// tipo, retirar sin reemplazo, fechas coherentes, activos dados de baja y
/// borrado lógico de capturas erróneas. Proveedor en memoria.
/// </summary>
public class ComponentsTests : IDisposable
{
    private readonly AssetFlowsHarness _h = AssetFlowsHarness.CreateInMemory();
    private readonly RecordingAuditLogService _audit = new();
    private readonly FakeCurrentUser _user = new();

    public ComponentsTests()
    {
        var now = DateTime.UtcNow;
        _h.Context.ComponentTypes.Add(new ComponentType { ComponentTypeId = 1, Code = "RAM", Name = "Memoria RAM", CreatedAt = now, UpdatedAt = now, Version = 1 });
        _h.Context.SaveChanges();
    }

    public void Dispose() => _h.Dispose();

    private Task<ComponentWriteResponse> Add(long assetId, string description, DateTime? at = null)
        => new AddComponentHandler(_h.Context, _user, _audit)
            .Handle(new AddComponentCommand(assetId, 1, description, null, at), default).AsTask();

    private Task<ComponentWriteResponse> Replace(long assetId, long id, int version, string description, DateTime? at = null)
        => new ReplaceComponentHandler(_h.Context, _user, _audit)
            .Handle(new ReplaceComponentCommand(assetId, id, version, description, "SN-2", at), default).AsTask();

    private Task<ComponentWriteResponse> Remove(long assetId, long id, int version, DateTime? at = null)
        => new RemoveComponentHandler(_h.Context, _user, _audit)
            .Handle(new RemoveComponentCommand(assetId, id, version, at), default).AsTask();

    private Task<IReadOnlyList<GetAssetComponentsResponse>> List(long assetId, bool removed)
        => new GetAssetComponentsHandler(_h.Context).Handle(new GetAssetComponentsQuery(assetId, removed), default).AsTask();

    [Fact]
    public async Task Reemplazar_cierra_el_actual_y_crea_uno_del_mismo_tipo()
    {
        var asset = _h.AddAsset(AssetStatusCodes.Asignado);
        var first = await Add(asset.AssetId, "8 GB DDR4", DateTime.UtcNow.AddDays(-30));

        var second = await Replace(asset.AssetId, first.AssetComponentId, first.Version, "16 GB DDR4");

        Assert.Equal(first.AssetComponentId, second.ReplacedAssetComponentId);
        var current = Assert.Single(await List(asset.AssetId, removed: false));
        Assert.Equal(("16 GB DDR4", "RAM"), (current.Description, current.ComponentTypeCode));
        var history = await List(asset.AssetId, removed: true);
        Assert.NotNull(history.Single(c => c.AssetComponentId == first.AssetComponentId).RemovedAt);
        Assert.Equal(new[] { "INSERT", "UPDATE", "INSERT" }, _audit.Entries.Select(e => e.Action));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Replace(asset.AssetId, first.AssetComponentId, first.Version, "32 GB"));
    }

    [Fact]
    public async Task Fechas_incoherentes_version_vieja_y_otro_equipo_rechazados()
    {
        var asset = _h.AddAsset(AssetStatusCodes.Disponible);
        var c = await Add(asset.AssetId, "8 GB", DateTime.UtcNow.AddDays(-1));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Replace(asset.AssetId, c.AssetComponentId, c.Version, "16 GB", DateTime.UtcNow.AddDays(-10)));
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => Remove(asset.AssetId, c.AssetComponentId, c.Version, DateTime.UtcNow.AddDays(-10)));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Remove(asset.AssetId, c.AssetComponentId, 9));

        var other = _h.AddAsset(AssetStatusCodes.Disponible);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Remove(other.AssetId, c.AssetComponentId, c.Version));
    }

    [Fact]
    public async Task Retirar_sin_reemplazo_y_segunda_vez_rechazado()
    {
        var asset = _h.AddAsset(AssetStatusCodes.Disponible);
        var c = await Add(asset.AssetId, "Tarjeta WiFi");

        await Remove(asset.AssetId, c.AssetComponentId, c.Version);
        Assert.Empty(await List(asset.AssetId, removed: false));
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Remove(asset.AssetId, c.AssetComponentId, c.Version));
    }

    [Fact]
    public async Task Activo_dado_de_baja_no_cambia_componentes_pero_se_corrige_captura()
    {
        var asset = _h.AddAsset(AssetStatusCodes.Disponible);
        var c = await Add(asset.AssetId, "SSD 512 GB");
        var status = _h.Context.AssetStatuses.Single(s => s.Code == AssetStatusCodes.DadoDeBaja);
        _h.Context.Assets.Single(a => a.AssetId == asset.AssetId).AssetStatusId = status.AssetStatusId;
        _h.Context.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Add(asset.AssetId, "HDD"));
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Remove(asset.AssetId, c.AssetComponentId, c.Version));

        var deleter = new SoftDeleteAuditor(_h.Context, _audit, _user, new FakeTransactionScopeFactory(), new RecordingLogger<SoftDeleteAuditor>());
        await new DeleteComponentHandler(_h.Context, deleter)
            .Handle(new DeleteComponentCommand(asset.AssetId, c.AssetComponentId, c.Version), default);

        Assert.Empty(await List(asset.AssetId, removed: true));
        Assert.Contains(_audit.Entries, e => e.Action == "DELETE" && e.TableName == "asset_components");
    }

    [Fact]
    public async Task Validador_exige_descripcion_o_serie_y_fecha_no_futura()
    {
        var validator = new AddComponentValidator(_h.Context);

        Assert.False((await validator.ValidateAsync(new AddComponentCommand(1, 1, " ", null, null))).IsValid);
        Assert.True((await validator.ValidateAsync(new AddComponentCommand(1, 1, null, "SN-1", null))).IsValid);
        Assert.False((await validator.ValidateAsync(new AddComponentCommand(1, 1, "RAM", null, DateTime.UtcNow.AddDays(5)))).IsValid);
        Assert.False((await validator.ValidateAsync(new AddComponentCommand(1, 99, "RAM", null, null))).IsValid);
    }
}
