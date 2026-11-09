using System.Text;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Pdf;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Assets.GetAssetLabels;
using TecnmInventario.Api.Features.Disposals.GetDisposalAct;

namespace TecnmInventario.Api.Tests;

/// <summary>Acta de baja y etiquetas QR: se generan PDF válidos con los datos del activo.</summary>
public class PdfDocumentsTests : IDisposable
{
    private readonly AssetFlowsHarness _h = AssetFlowsHarness.CreateInMemory();

    public PdfDocumentsTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public void Dispose() => _h.Dispose();

    private static void AssertPdf(GeneratedFile file)
    {
        Assert.Equal(GeneratedFile.Pdf, file.ContentType);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
        Assert.True(file.Content.Length > 1000);
    }

    [Fact]
    public async Task Acta_de_baja_genera_pdf_con_el_folio_en_el_nombre()
    {
        var asset = _h.AddAsset(AssetStatusCodes.DadoDeBaja);
        var now = DateTime.UtcNow;
        _h.Context.AddRange(
            new DisposalReason { DisposalReasonId = 1, Code = "OBSOLETO", Name = "Obsolescencia", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetDisposal { AssetDisposalId = 7, AssetId = asset.AssetId, DisposalReasonId = 1, Folio = "BAJA-2026-007", Dictamen = "Tarjeta madre dañada; reparación incosteable.", DisposedAt = DateOnly.FromDateTime(now), CreatedAt = now, UpdatedAt = now, CreatedBy = 1, Version = 1 });
        _h.Context.SaveChanges();

        var file = await new GetDisposalActHandler(_h.Context).Handle(new GetDisposalActQuery(7), default);

        AssertPdf(file);
        Assert.Equal("acta-baja-BAJA-2026-007.pdf", file.FileName);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new GetDisposalActHandler(_h.Context).Handle(new GetDisposalActQuery(999), default).AsTask());
    }

    [Fact]
    public async Task Etiquetas_genera_pdf_e_ignora_inexistentes()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => _h.AddAsset(AssetStatusCodes.Disponible).AssetId).Append(999_999).ToList();

        AssertPdf(await new GetAssetLabelsHandler(_h.Context).Handle(new GetAssetLabelsQuery(ids), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new GetAssetLabelsHandler(_h.Context).Handle(new GetAssetLabelsQuery(new long[] { 999_999 }), default).AsTask());
    }

    [Fact]
    public void Etiquetas_validador_pide_entre_1_y_90()
    {
        var v = new GetAssetLabelsValidator();
        Assert.False(v.Validate(new GetAssetLabelsQuery(Array.Empty<long>())).IsValid);
        Assert.False(v.Validate(new GetAssetLabelsQuery(Enumerable.Range(1, 91).Select(i => (long)i).ToList())).IsValid);
        Assert.True(v.Validate(new GetAssetLabelsQuery(new long[] { 1, 2 })).IsValid);
    }
}
